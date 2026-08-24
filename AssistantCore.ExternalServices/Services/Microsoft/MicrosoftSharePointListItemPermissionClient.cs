using System.Net.Http.Headers;
using System.Text.Json;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftSharePointListItemPermissionClient(HttpClient httpClient)
{
    private const string RoleAssignmentsQuery =
        "?$select=Member/Id,Member/Title,Member/LoginName,Member/PrincipalType,Member/AadObjectId," +
        "RoleDefinitionBindings/Id,RoleDefinitionBindings/Name,RoleDefinitionBindings/RoleTypeKind" +
        "&$expand=Member,RoleDefinitionBindings";

    public async Task<MicrosoftSharePointListItemPermissionReadResult> GetPermissionsAsync(
        string siteUrl,
        string accessToken,
        Guid listId,
        int itemId,
        CancellationToken cancellationToken = default)
    {
        var siteUri = CreateSiteUri(siteUrl);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("SharePoint access token is required.", nameof(accessToken));
        }

        if (listId == Guid.Empty)
        {
            throw new ArgumentException("SharePoint list identifier is required.", nameof(listId));
        }

        if (itemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemId), "SharePoint list item identifier must be positive.");
        }

        var listPath = $"_api/web/lists(guid'{listId:D}')";
        var itemPath = $"{listPath}/items({itemId})";
        var itemInheritance = await ReadInheritanceAsync(
            new Uri(siteUri, $"{itemPath}?$select=HasUniqueRoleAssignments"),
            accessToken,
            cancellationToken);
        if (itemInheritance is null)
        {
            return PartialResponse();
        }

        if (itemInheritance.Value)
        {
            return await ReadRoleAssignmentsAsync(
                siteUri,
                new Uri(siteUri, $"{itemPath}/RoleAssignments{RoleAssignmentsQuery}"),
                accessToken,
                MicrosoftSharePointPermissionInheritanceSource.ListItem,
                cancellationToken);
        }

        var listInheritance = await ReadInheritanceAsync(
            new Uri(siteUri, $"{listPath}?$select=HasUniqueRoleAssignments"),
            accessToken,
            cancellationToken);
        if (listInheritance is null)
        {
            return PartialResponse();
        }

        if (listInheritance.Value)
        {
            return await ReadRoleAssignmentsAsync(
                siteUri,
                new Uri(siteUri, $"{listPath}/RoleAssignments{RoleAssignmentsQuery}"),
                accessToken,
                MicrosoftSharePointPermissionInheritanceSource.List,
                cancellationToken);
        }

        return await ReadRoleAssignmentsAsync(
            siteUri,
            new Uri(siteUri, $"_api/web/RoleAssignments{RoleAssignmentsQuery}"),
            accessToken,
            MicrosoftSharePointPermissionInheritanceSource.Site,
            cancellationToken);
    }

    private async Task<bool?> ReadInheritanceAsync(
        Uri requestUri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync(requestUri, accessToken, cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var payload = UnwrapVerbosePayload(document.RootElement);
        return payload.TryGetProperty("HasUniqueRoleAssignments", out var inheritance)
            && inheritance.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? inheritance.GetBoolean()
                : null;
    }

    private async Task<MicrosoftSharePointListItemPermissionReadResult> ReadRoleAssignmentsAsync(
        Uri siteUri,
        Uri firstPageUri,
        string accessToken,
        MicrosoftSharePointPermissionInheritanceSource inheritanceSource,
        CancellationToken cancellationToken)
    {
        var permissions = new List<MicrosoftSharePointPermission>();
        Uri? pageUri = firstPageUri;

        while (pageUri is not null)
        {
            using var document = await GetJsonAsync(pageUri, accessToken, cancellationToken);
            if (!TryReadCollection(document.RootElement, out var assignments, out var nextLink))
            {
                return PartialResponse();
            }

            foreach (var assignment in assignments.EnumerateArray())
            {
                var mapping = TryMapPermission(assignment);
                if (mapping.PartialResponse)
                {
                    return PartialResponse();
                }

                if (mapping.Permission is null)
                {
                    return new MicrosoftSharePointListItemPermissionReadResult.Unresolved(
                        MicrosoftSharePointPermissionUnresolvedReason.UnknownPrincipal);
                }

                permissions.Add(mapping.Permission);
            }

            pageUri = CreateNextPageUri(siteUri, nextLink);
            if (nextLink is not null && pageUri is null)
            {
                return PartialResponse();
            }
        }

        return new MicrosoftSharePointListItemPermissionReadResult.Resolved(
            inheritanceSource,
            permissions);
    }

    private async Task<JsonDocument> GetJsonAsync(
        Uri requestUri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json;odata=nometadata");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"SharePoint permissions request failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw new MicrosoftExternalException("SharePoint permissions response contained invalid JSON.");
        }
    }

    private static (MicrosoftSharePointPermission? Permission, bool PartialResponse) TryMapPermission(
        JsonElement assignment)
    {
        if (assignment.ValueKind != JsonValueKind.Object
            || !assignment.TryGetProperty("Member", out var member)
            || member.ValueKind != JsonValueKind.Object
            || !assignment.TryGetProperty("RoleDefinitionBindings", out var bindings)
            || !TryUnwrapArray(bindings, out var roleDefinitions))
        {
            return (null, true);
        }

        if (!TryGetInt32(member, "Id", out var principalId)
            || !TryGetInt32(member, "PrincipalType", out var principalType)
            || principalId <= 0
            || principalType <= 0)
        {
            return (null, false);
        }

        if (!TryGetRequiredString(member, "Title", out var title))
        {
            return (null, true);
        }

        var mappedRoles = new List<MicrosoftSharePointRoleDefinition>();
        foreach (var roleDefinition in roleDefinitions.EnumerateArray())
        {
            if (roleDefinition.ValueKind != JsonValueKind.Object
                || !TryGetInt32(roleDefinition, "Id", out var roleId)
                || !TryGetRequiredString(roleDefinition, "Name", out var roleName)
                || !TryGetInt32(roleDefinition, "RoleTypeKind", out var roleTypeKind))
            {
                return (null, true);
            }

            mappedRoles.Add(new MicrosoftSharePointRoleDefinition(roleId, roleName, roleTypeKind));
        }

        var loginName = member.TryGetProperty("LoginName", out var loginNameElement)
            && loginNameElement.ValueKind == JsonValueKind.String
                ? loginNameElement.GetString()
                : null;
        var entraObjectId = TryGetEntraObjectId(member);
        var principal = new MicrosoftSharePointPrincipal(
            principalId,
            entraObjectId,
            title,
            loginName,
            principalType);
        return (new MicrosoftSharePointPermission(principal, mappedRoles), false);
    }

    private static string? TryGetEntraObjectId(JsonElement member)
    {
        if (!member.TryGetProperty("AadObjectId", out var aadObjectId))
        {
            return null;
        }

        return aadObjectId.ValueKind switch
        {
            JsonValueKind.String => aadObjectId.GetString(),
            JsonValueKind.Object => TryGetString(aadObjectId, "NameId"),
            _ => null
        };
    }

    private static bool TryReadCollection(
        JsonElement root,
        out JsonElement collection,
        out string? nextLink)
    {
        collection = default;
        nextLink = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var payload = UnwrapVerbosePayload(root);
        if (!TryUnwrapArray(payload, out collection))
        {
            return false;
        }

        nextLink = TryGetString(payload, "@odata.nextLink")
            ?? TryGetString(payload, "__next")
            ?? TryGetString(root, "@odata.nextLink");
        return true;
    }

    private static JsonElement UnwrapVerbosePayload(JsonElement root) =>
        root.TryGetProperty("d", out var payload) && payload.ValueKind == JsonValueKind.Object
            ? payload
            : root;

    private static bool TryUnwrapArray(JsonElement value, out JsonElement array)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            array = value;
            return true;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("value", out array) && array.ValueKind == JsonValueKind.Array)
            {
                return true;
            }

            if (value.TryGetProperty("results", out array) && array.ValueKind == JsonValueKind.Array)
            {
                return true;
            }
        }

        array = default;
        return false;
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = default;
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryGetRequiredString(
        JsonElement element,
        string propertyName,
        out string value)
    {
        value = TryGetString(element, propertyName) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static Uri? CreateNextPageUri(Uri siteUri, string? nextLink)
    {
        if (nextLink is null)
        {
            return null;
        }

        if (!Uri.TryCreate(siteUri, nextLink, out var nextPageUri)
            || nextPageUri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(nextPageUri.Authority, siteUri.Authority, StringComparison.OrdinalIgnoreCase)
            || !nextPageUri.AbsolutePath.StartsWith(siteUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return nextPageUri;
    }

    private static Uri CreateSiteUri(string siteUrl)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri)
            || siteUri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(siteUri.Query)
            || !string.IsNullOrEmpty(siteUri.Fragment))
        {
            throw new ArgumentException("SharePoint site URL must be an HTTPS URL without query or fragment.", nameof(siteUrl));
        }

        return new Uri($"{siteUri.GetLeftPart(UriPartial.Path).TrimEnd('/')}/");
    }

    private static MicrosoftSharePointListItemPermissionReadResult PartialResponse() =>
        new MicrosoftSharePointListItemPermissionReadResult.Unresolved(
            MicrosoftSharePointPermissionUnresolvedReason.PartialResponse);
}
