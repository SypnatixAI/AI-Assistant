using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Members;
using AssistantCore.Service.Application.Services.Members;

namespace AssistantCore.Service.Application.Commands.UpdateMemberStatus;

public sealed class UpdateMemberStatusCommandHandler(
    IMemberManagementService memberManagementService) : IRequestHandler<UpdateMemberStatusCommand, MemberResponse>
{
    public async Task<MemberResponse> HandleAsync(
        UpdateMemberStatusCommand request,
        CancellationToken cancellationToken)
    {
        var member = await memberManagementService.UpdateMemberStatusAsync(
            request.MemberId,
            request.Status,
            request.ExpectedVersion,
            cancellationToken);

        return MemberResponse.FromMember(member);
    }
}
