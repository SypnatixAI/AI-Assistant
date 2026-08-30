using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Commands.DeleteConversation;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class DeleteConversationCommandHandlerTests
{
    [Theory]
    [InlineAutoDomainData(true)]
    [InlineAutoDomainData(false)]
    public async Task Given_ADeletableConversation_When_HandleAsync_Then_ReportsWhetherItWasAlreadyDeleted(
        bool alreadyDeleted,
        Organization organization,
        OrganizationMember member,
        Guid conversationId)
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService(alreadyDeleted);
        var handler = CreateHandler(organization, member, lifecycleService);

        // When
        var response = await handler.HandleAsync(
            new DeleteConversationCommand(conversationId),
            CancellationToken.None);

        // Then
        Assert.Equal(conversationId, response.ConversationId);
        Assert.Equal(alreadyDeleted, response.AlreadyDeleted);
        Assert.Equal(organization.Id, lifecycleService.ReceivedOrganizationId);
        Assert.Equal(member.Id, lifecycleService.ReceivedOwnerMemberId);
        Assert.Equal(conversationId, lifecycleService.ReceivedConversationId);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptyConversationIdentifier_When_HandleAsync_Then_ThrowsBadRequestException(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService(alreadyDeleted: false);
        var handler = CreateHandler(organization, member, lifecycleService);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.HandleAsync(
                new DeleteConversationCommand(Guid.Empty),
                CancellationToken.None));
        Assert.False(lifecycleService.WasCalled);
    }

    private static DeleteConversationCommandHandler CreateHandler(
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

    private sealed class RecordingConversationLifecycleService(bool alreadyDeleted)
        : IConversationLifecycleService
    {
        public bool WasCalled { get; private set; }

        public Guid ReceivedOrganizationId { get; private set; }

        public Guid ReceivedOwnerMemberId { get; private set; }

        public Guid ReceivedConversationId { get; private set; }

        public Task<ConversationResponse> UpdateAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            string? title,
            string? status,
            int? expectedVersion,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedOrganizationId = organizationId;
            ReceivedOwnerMemberId = ownerMemberId;
            ReceivedConversationId = conversationId;
            return Task.FromResult(alreadyDeleted);
        }
    }
}
