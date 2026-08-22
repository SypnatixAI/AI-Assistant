namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365Drive : Microsoft365Source
{
    public Guid OrganizationId { get; set; }

    public Guid OrganizationConnectorId { get; set; }

    public string SiteId { get; set; } = string.Empty;

    public string DriveId { get; set; } = string.Empty;

    public Organization Organization { get; set; } = null!;

    public OrganizationConnector OrganizationConnector { get; set; } = null!;
}
