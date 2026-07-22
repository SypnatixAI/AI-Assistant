using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class Organization
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string MicrosoftTenantId { get; set; } = string.Empty;

    public RecordStatus Status { get; set; }

    public ICollection<OrganizationMember> Members { get; set; } = [];
}
