using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Commands.UpdateConversation;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class UpdateConversationCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_APatch_When_HandleAsync_Then_PassesTheAuthenticatedContextToTheService(
        Organization organization,
        OrganizationMember member,
        Guid conversationId,
        ConversationResponse response)
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService { Response = response };
        var handler = CreateHandler(organization, member, lifecycleService);

        // When
        var result = await handler.HandleAsync(
            new UpdateConversationCommand(conversationId, "Nouveau titre", "Archived", 7),
            CancellationToken.None);

        // Then
        Assert.Same(response, result);
        Assert.Equal(organization.Id, lifecycleService.ReceivedOrganizationId);
        Assert.Equal(member.Id, lifecycleService.ReceivedOwnerMemberId);
        Assert.Equal(conversationId, lifecycleService.ReceivedConversationId);
        Assert.Equal("Nouveau titre", lifecycleService.ReceivedTitle);
        Assert.Equal("Archived", lifecycleService.ReceivedStatus);
        Assert.Equal(7, lifecycleService.ReceivedExpectedVersion);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyConversationIdentifier_When_HandleAsync_Then_ThrowsBadRequestException(
        Organization organization,
        OrganizationMember member,
        ConversationResponse response)
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService { Response = response };
        var handler = CreateHandler(organization, member, lifecycleService);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.HandleAsync(
                new UpdateConversationCommand(Guid.Empty, "Titre", null, null),
                CancellationToken.None));
        Assert.False(lifecycleService.WasCalled);
    }

    private static UpdateConversationCommandHandler CreateHandler(
        Organization organization,
        OrganizationMember member,
        IConversationLifecycleService lifecycleService) =>
        new(
            new StubUserContextService(new MessageUserContext(organization, member)),
            lifecycleService);

    private sealed class StubUserContextService(MessageUserContext context)
        : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(context);
    }

    private sealed class RecordingConversationLifecycleService : IConversationLifecycleService
    {
        public required ConversationResponse Response { get; init; }

        public bool WasCalled { get; private set; }

        public Guid ReceivedOrganizationId { get; private set; }

        public Guid ReceivedOwnerMemberId { get; private set; }

        public Guid ReceivedConversationId { get; private set; }

        public string? ReceivedTitle { get; private set; }

        public string? ReceivedStatus { get; private set; }

        public int? ReceivedExpectedVersion { get; private set; }

        public Task<ConversationResponse> UpdateAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            string? title,
            string? status,
            int? expectedVersion,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedOrganizationId = organizationId;
            ReceivedOwnerMemberId = ownerMemberId;
            ReceivedConversationId = conversationId;
            ReceivedTitle = title;
            ReceivedStatus = status;
            ReceivedExpectedVersion = expectedVersion;
            return Task.FromResult(Response);
        }

        public Task<bool> DeleteAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
