using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public IdentityProvider IdentityProvider { get; set; }

    public string ExternalTenantId { get; set; } = string.Empty;

    public RecordStatus Status { get; set; }

    public ICollection<OrganizationMember> Members { get; set; } = [];

    public ICollection<OrganizationConnector> Connectors { get; set; } = [];
}
