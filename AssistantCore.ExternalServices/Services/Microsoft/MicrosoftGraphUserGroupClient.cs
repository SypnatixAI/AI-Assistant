using System.Text.Json.Serialization;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphUserGroupClient(HttpClient httpClient)
{
    private readonly MicrosoftGraphCollectionReader collectionReader = new(httpClient);

    public Task<IReadOnlyCollection<string>> GetTransitiveGroupIdsAsync(
        string graphBaseUrl,
        string accessToken,
        string userId,
        CancellationToken cancellationToken = default) =>
        collectionReader.ReadAsync<Group, string>(
            CreateGroupsUri(graphBaseUrl, userId),
            accessToken,
            MapGroupId,
            "user transitive group memberships",
            cancellationToken);

    private static Uri CreateGroupsUri(string graphBaseUrl, string userId)
    {
        if (!Guid.TryParse(userId, out var parsedUserId) || parsedUserId == Guid.Empty)
        {
            throw new ArgumentException("A valid Microsoft Entra user identifier is required.", nameof(userId));
        }

        if (!Uri.TryCreate(graphBaseUrl, UriKind.Absolute, out var graphBaseUri)
            || graphBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        var normalizedBaseUri = new Uri($"{graphBaseUri.GetLeftPart(UriPartial.Authority)}/");
        return new Uri(
            normalizedBaseUri,
            $"v1.0/users/{parsedUserId:D}/transitiveMemberOf/microsoft.graph.group?$select=id");
    }

    private static string MapGroupId(Group group)
    {
        if (!Guid.TryParse(group.Id, out var groupId) || groupId == Guid.Empty)
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph returned an invalid group identifier.");
        }

        return groupId.ToString("D");
    }

    private sealed record Group(
        [property: JsonPropertyName("id")] string? Id);
}
