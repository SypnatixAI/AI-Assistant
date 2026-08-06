using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Members;
using AssistantCore.Service.Application.Services.Members;

namespace AssistantCore.Service.Application.Commands.UpdateMemberRole;

public sealed class UpdateMemberRoleCommandHandler(
    IMemberManagementService memberManagementService) : IRequestHandler<UpdateMemberRoleCommand, MemberResponse>
{
    public async Task<MemberResponse> HandleAsync(
        UpdateMemberRoleCommand request,
        CancellationToken cancellationToken)
    {
        var member = await memberManagementService.UpdateMemberRoleAsync(
            request.MemberId,
            request.Role,
            cancellationToken);

        return MemberResponse.FromMember(member);
    }
}
