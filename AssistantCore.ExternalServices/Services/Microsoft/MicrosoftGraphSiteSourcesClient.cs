using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphSiteSourcesClient(HttpClient httpClient)
{
    private const string DrivesSelect = "$select=id,name,webUrl,driveType";
    private const string ListsSelect = "$select=id,displayName,webUrl,list,system";
    private readonly MicrosoftGraphCollectionReader collectionReader = new(httpClient);

    public Task<IReadOnlyCollection<MicrosoftDrive>> GetSiteDrivesAsync(
        string graphBaseUrl,
        string accessToken,
        string siteId,
        CancellationToken cancellationToken = default) =>
        collectionReader.ReadAsync<DriveItem, MicrosoftDrive>(
            CreateSiteCollectionUri(graphBaseUrl, siteId, "drives", DrivesSelect),
            accessToken,
            MapDrive,
            "site drives",
            cancellationToken);

    public Task<IReadOnlyCollection<MicrosoftList>> GetSiteListsAsync(
        string graphBaseUrl,
        string accessToken,
        string siteId,
        CancellationToken cancellationToken = default) =>
        collectionReader.ReadAsync<ListItem, MicrosoftList>(
            CreateSiteCollectionUri(graphBaseUrl, siteId, "lists", ListsSelect),
            accessToken,
            MapList,
            "site lists",
            cancellationToken);

    private static Uri CreateSiteCollectionUri(
        string graphBaseUrl,
        string siteId,
        string collection,
        string select)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw new ArgumentException("Microsoft Graph site identifier is required.", nameof(siteId));
        }

        if (!Uri.TryCreate(graphBaseUrl, UriKind.Absolute, out var graphBaseUri)
            || graphBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        var normalizedBaseUri = new Uri($"{graphBaseUri.GetLeftPart(UriPartial.Authority)}/");
        var escapedSiteId = Uri.EscapeDataString(siteId);
        return new Uri(normalizedBaseUri, $"v1.0/sites/{escapedSiteId}/{collection}?{select}");
    }

    private static MicrosoftDrive MapDrive(DriveItem drive)
    {
        if (string.IsNullOrWhiteSpace(drive.Id) || string.IsNullOrWhiteSpace(drive.Name))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph site drives response contained an invalid drive.");
        }

        return new MicrosoftDrive(drive.Id, drive.Name, drive.WebUrl, drive.DriveType);
    }

    private static MicrosoftList MapList(ListItem list)
    {
        if (string.IsNullOrWhiteSpace(list.Id) || string.IsNullOrWhiteSpace(list.DisplayName))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph site lists response contained an invalid list.");
        }

        return new MicrosoftList(
            list.Id,
            list.DisplayName,
            list.WebUrl,
            list.List?.Hidden ?? false,
            list.List?.Template,
            HasFacet(list.System),
            HasFacet(list.Deleted));
    }

    private static bool HasFacet(JsonElement? facet) =>
        facet is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
        && facet.Value.ValueKind != JsonValueKind.False;

    private sealed record DriveItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("webUrl")] string? WebUrl,
        [property: JsonPropertyName("driveType")] string? DriveType);

    private sealed record ListItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("webUrl")] string? WebUrl,
        [property: JsonPropertyName("list")] ListInformation? List,
        [property: JsonPropertyName("system")] JsonElement? System,
        [property: JsonPropertyName("deleted")] JsonElement? Deleted);

    private sealed record ListInformation(
        [property: JsonPropertyName("hidden")] bool Hidden,
        [property: JsonPropertyName("template")] string? Template);
}
