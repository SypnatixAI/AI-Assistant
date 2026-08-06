using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Application.Models.Members;

public sealed record MemberResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string Role,
    string Status)
{
    public static MemberResponse FromMember(OrganizationMember member) =>
        new(
            member.Id,
            member.Name,
            member.Email,
            member.Role.ToString(),
            member.Status.ToString());
}
