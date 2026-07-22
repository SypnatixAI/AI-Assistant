using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

public sealed class OrganizationMember
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string MicrosoftIdentifier { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public RecordStatus Status { get; set; }

    public Organization Organization { get; set; } = null!;
}
