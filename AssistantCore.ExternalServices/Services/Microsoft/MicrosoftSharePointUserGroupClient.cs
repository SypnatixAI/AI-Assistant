using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftSharePointUserGroupClient(HttpClient httpClient)
{
    public async Task<IReadOnlyCollection<string>> GetGroupIdsAsync(
        string siteUrl,
        string accessToken,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        var siteUri = ValidateSiteUrl(siteUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(userEmail);

        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        Uri? nextPage = CreateGroupsUri(siteUri, userEmail);
        while (nextPage is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextPage);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.ParseAdd("application/json;odata=nometadata");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new MicrosoftExternalException(
                    $"Microsoft SharePoint rejected a user group membership request with status {(int)response.StatusCode}.",
                    statusCode: response.StatusCode);
            }

            SharePointGroupsResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<SharePointGroupsResponse>(
                    cancellationToken: cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new MicrosoftExternalException(
                    "Microsoft SharePoint returned an invalid user group membership response.",
                    exception);
            }

            if (payload?.Value is null)
            {
                throw new MicrosoftExternalException(
                    "Microsoft SharePoint returned an empty user group membership response.");
            }

            foreach (var group in payload.Value)
            {
                if (group.Id <= 0)
                {
                    throw new MicrosoftExternalException(
                        "Microsoft SharePoint returned an invalid local group identifier.");
                }

                groupIds.Add(group.Id.ToString(CultureInfo.InvariantCulture));
            }

            nextPage = ResolveNextPage(siteUri, payload.NextLink ?? payload.LegacyNextLink);
        }

        return groupIds.OrderBy(groupId => groupId, StringComparer.Ordinal).ToArray();
    }

    private static Uri ValidateSiteUrl(string siteUrl)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri)
            || siteUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft SharePoint site URL must use HTTPS.", nameof(siteUrl));
        }

        return siteUri;
    }

    private static Uri CreateGroupsUri(Uri siteUri, string userEmail)
    {
        var escapedEmail = Uri.EscapeDataString(userEmail.Trim().Replace("'", "''", StringComparison.Ordinal));
        return new Uri(
            $"{siteUri.AbsoluteUri.TrimEnd('/')}/_api/web/siteusers/getbyemail('{escapedEmail}')/groups?$select=Id");
    }

    private static Uri? ResolveNextPage(Uri siteUri, string? nextLink)
    {
        if (string.IsNullOrWhiteSpace(nextLink))
        {
            return null;
        }

        if (!Uri.TryCreate(nextLink, UriKind.Absolute, out var nextPage)
            || !string.Equals(nextPage.Scheme, siteUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(nextPage.Authority, siteUri.Authority, StringComparison.OrdinalIgnoreCase)
            || !nextPage.AbsolutePath.StartsWith(
                $"{siteUri.AbsolutePath.TrimEnd('/')}/_api/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new MicrosoftExternalException(
                "Microsoft SharePoint returned an invalid user group membership continuation URL.");
        }

        return nextPage;
    }

    private sealed record SharePointGroupsResponse(
        [property: JsonPropertyName("value")] IReadOnlyCollection<SharePointGroup>? Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink,
        [property: JsonPropertyName("odata.nextLink")] string? LegacyNextLink);

    private sealed record SharePointGroup(
        [property: JsonPropertyName("Id")] int Id);
}
