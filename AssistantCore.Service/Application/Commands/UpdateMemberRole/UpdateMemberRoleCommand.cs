using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Members;

namespace AssistantCore.Service.Application.Commands.UpdateMemberRole;

public sealed record UpdateMemberRoleCommand(
    Guid MemberId,
    string Role) : IRequest<MemberResponse>;
