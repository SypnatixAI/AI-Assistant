namespace AssistantCore.Service.Application.Models.Microsoft365.Permissions;

public sealed record Microsoft365ContentReference(
    Microsoft365ContentReferenceKind Kind,
    string? SiteId,
    string? DriveId,
    string? ListId,
    string ItemId,
    string? SiteUrl = null)
{
    public string? SiteId { get; } = NormalizeSiteId(Kind, SiteId);

    public string? DriveId { get; } = NormalizeOptionalId(DriveId);

    public string? ListId { get; } = NormalizeOptionalId(ListId);

    public string ItemId { get; } = string.IsNullOrWhiteSpace(ItemId)
        ? throw new ArgumentException("The item id is required.", nameof(ItemId))
        : ItemId;

    public string? SiteUrl { get; } = NormalizeOptionalId(SiteUrl);

    private static string? NormalizeSiteId(
        Microsoft365ContentReferenceKind kind,
        string? siteId)
    {
        var normalized = NormalizeOptionalId(siteId);
        if (kind == Microsoft365ContentReferenceKind.ListItem && normalized is null)
        {
            throw new ArgumentException("The site id is required for list items.", nameof(siteId));
        }

        return normalized;
    }

    private static string? NormalizeOptionalId(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}
