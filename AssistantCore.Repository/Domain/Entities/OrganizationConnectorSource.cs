using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class OrganizationConnectorSource
{
    public Guid OrganizationConnectorId { get; set; }

    public Microsoft365SourceType SourceType { get; set; }

    public RecordStatus Status { get; set; }

    public bool IsIndexed { get; set; }

    public OrganizationConnector OrganizationConnector { get; set; } = null!;
}
