using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.UpdateMemberStatus;

namespace AssistantCore.Service.Tests.Members;

public sealed class UpdateMemberStatusCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnUpdatedMember_When_HandleAsync_Then_MapsResponseAndPropagatesRequest(
        CancellationToken cancellationToken,
        OrganizationMember member,
        int expectedVersion)
    {
        // Given
        member.Status = RecordStatus.Inactive;
        member.Version = expectedVersion;
        var service = new StubMemberManagementService { UpdatedMember = member };
        var handler = new UpdateMemberStatusCommandHandler(service);
        var command = new UpdateMemberStatusCommand(member.Id, "Inactive", expectedVersion);

        // When
        var response = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Equal(member.Id, response.Id);
        Assert.Equal(member.Name, response.DisplayName);
        Assert.Equal(member.Email, response.Email);
        Assert.Equal("Inactive", response.Status);
        Assert.Equal(expectedVersion, response.Version);
        Assert.Equal(member.Id, service.ReceivedMemberId);
        Assert.Equal("Inactive", service.ReceivedStatus);
        Assert.Equal(expectedVersion, service.ReceivedExpectedVersion);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }
}
