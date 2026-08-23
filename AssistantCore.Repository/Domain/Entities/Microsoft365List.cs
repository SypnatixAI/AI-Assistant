namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365List : Microsoft365Source
{
    public Guid OrganizationId { get; set; }

    public Guid OrganizationConnectorId { get; set; }

    public string SiteId { get; set; } = string.Empty;

    public string ListId { get; set; } = string.Empty;

    public string? SchemaFingerprint { get; set; }

    public bool RequiresItemReprocessing { get; set; }

    public Organization Organization { get; set; } = null!;

    public OrganizationConnector OrganizationConnector { get; set; } = null!;
}
