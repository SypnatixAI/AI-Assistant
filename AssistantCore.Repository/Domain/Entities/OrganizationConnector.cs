using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class OrganizationConnector
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public ConnectorType Type { get; set; }

    public RecordStatus Status { get; set; }

    public bool IsConfigured { get; set; }

    public Organization Organization { get; set; } = null!;

    public ICollection<OrganizationConnectorSource> Sources { get; set; } = [];

    public Microsoft365Connection? Microsoft365Connection { get; set; }
}
