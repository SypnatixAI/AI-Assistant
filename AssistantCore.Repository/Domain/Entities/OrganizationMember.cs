using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class OrganizationMember
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public IdentityProvider IdentityProvider { get; set; }

    public string ExternalUserId { get; set; } = string.Empty;

    public OrganizationRole Role { get; set; }

    public RecordStatus Status { get; set; }

    public Organization Organization { get; set; } = null!;
}
