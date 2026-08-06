using AssistantCore.Service.Application.Models.Members;

namespace AssistantCore.Service.Application.Commands.GetMembers.Models;

public sealed record GetMembersResponse(IReadOnlyCollection<MemberResponse> Members);
