using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.GetMembers;

namespace AssistantCore.Service.Tests.Members;

public sealed class GetMembersCommandHandlerTests
{
    [Fact]
    public async Task Given_Members_When_HandleAsync_Then_MapsResponseAndPropagatesCancellationToken()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var admin = CreateMember(OrganizationRole.Admin, RecordStatus.Active);
        var inactiveUser = CreateMember(OrganizationRole.User, RecordStatus.Inactive);
        var service = new StubMemberManagementService { Members = [admin, inactiveUser] };
        var handler = new GetMembersCommandHandler(service);

        // When
        var response = await handler.HandleAsync(new GetMembersCommand(), cancellationToken);

        // Then
        Assert.Collection(
            response.Members,
            member => AssertMember(member, admin),
            member => AssertMember(member, inactiveUser));
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Given_NoMembers_When_HandleAsync_Then_ReturnsEmptyCollection()
    {
        // Given
        var handler = new GetMembersCommandHandler(new StubMemberManagementService());

        // When
        var response = await handler.HandleAsync(new GetMembersCommand(), CancellationToken.None);

        // Then
        Assert.Empty(response.Members);
    }

    private static OrganizationMember CreateMember(
        OrganizationRole role,
        RecordStatus status) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Name = $"{role} Member",
        Email = $"{role.ToString().ToLowerInvariant()}@example.com",
        Role = role,
        Status = status
    };

    private static void AssertMember(
        Application.Models.Members.MemberResponse response,
        OrganizationMember member)
    {
        Assert.Equal(member.Id, response.Id);
        Assert.Equal(member.Name, response.DisplayName);
        Assert.Equal(member.Email, response.Email);
        Assert.Equal(member.Role.ToString(), response.Role);
        Assert.Equal(member.Status.ToString(), response.Status);
    }
}
