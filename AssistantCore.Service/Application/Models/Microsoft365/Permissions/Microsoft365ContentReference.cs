namespace AssistantCore.Service.Application.Models.Microsoft365.Permissions;

public sealed record Microsoft365ContentReference(
    Microsoft365ContentReferenceKind Kind,
    string SiteId,
    string? DriveId,
    string? ListId,
    string ItemId,
    string? SiteUrl = null)
{
    public string SiteId { get; } = string.IsNullOrWhiteSpace(SiteId)
        ? throw new ArgumentException("The site id is required.", nameof(SiteId))
        : SiteId;

    public string? DriveId { get; } = NormalizeOptionalId(DriveId);

    public string? ListId { get; } = NormalizeOptionalId(ListId);

    public string ItemId { get; } = string.IsNullOrWhiteSpace(ItemId)
        ? throw new ArgumentException("The item id is required.", nameof(ItemId))
        : ItemId;

    public string? SiteUrl { get; } = NormalizeOptionalId(SiteUrl);

    private static string? NormalizeOptionalId(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }
}
