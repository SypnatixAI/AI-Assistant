using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Members;

namespace AssistantCore.Service.Application.Commands.UpdateMemberStatus;

public sealed record UpdateMemberStatusCommand(
    Guid MemberId,
    string Status,
    int? ExpectedVersion) : IRequest<MemberResponse>;
