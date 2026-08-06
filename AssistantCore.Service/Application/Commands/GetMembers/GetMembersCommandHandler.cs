using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMembers.Models;
using AssistantCore.Service.Application.Models.Members;
using AssistantCore.Service.Application.Services.Members;

namespace AssistantCore.Service.Application.Commands.GetMembers;

public sealed class GetMembersCommandHandler(
    IMemberManagementService memberManagementService) : IRequestHandler<GetMembersCommand, GetMembersResponse>
{
    public async Task<GetMembersResponse> HandleAsync(
        GetMembersCommand request,
        CancellationToken cancellationToken)
    {
        var members = await memberManagementService.GetMembersAsync(cancellationToken);
        return new GetMembersResponse(members.Select(MemberResponse.FromMember).ToList());
    }
}
