using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.UpdateMemberRole;

namespace AssistantCore.Service.Tests.Members;

public sealed class UpdateMemberRoleCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnUpdatedMember_When_HandleAsync_Then_MapsResponseAndPropagatesRequest(
        CancellationToken cancellationToken,
        OrganizationMember member)
    {
        // Given
        member.Role = OrganizationRole.Admin;
        member.Status = RecordStatus.Active;
        var service = new StubMemberManagementService { UpdatedMember = member };
        var handler = new UpdateMemberRoleCommandHandler(service);
        var command = new UpdateMemberRoleCommand(member.Id, "Admin");

        // When
        var response = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Equal(member.Id, response.Id);
        Assert.Equal(member.Name, response.DisplayName);
        Assert.Equal(member.Email, response.Email);
        Assert.Equal("Admin", response.Role);
        Assert.Equal("Active", response.Status);
        Assert.Equal(member.Id, service.ReceivedMemberId);
        Assert.Equal("Admin", service.ReceivedRole);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }
}
