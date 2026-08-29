using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Commands.GetConversationMessages;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Conversations.Pagination;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class GetConversationMessagesCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AnEmptyConversationId_When_HandleAsync_Then_ThrowsBadRequestException(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        var handler = CreateHandler(organization, member, listingService);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.HandleAsync(
                new GetConversationMessagesCommand(Guid.Empty, null, null),
                CancellationToken.None));
        Assert.False(listingService.WasCalled);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidCursor_When_HandleAsync_Then_ThrowsBadRequestException(
        Organization organization,
        OrganizationMember member,
        Guid conversationId)
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        var handler = CreateHandler(organization, member, listingService);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.HandleAsync(
                new GetConversationMessagesCommand(conversationId, null, "not-a-valid-cursor"),
                CancellationToken.None));
        Assert.False(listingService.WasCalled);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACursorFromAnotherConversation_When_HandleAsync_Then_ThrowsBadRequestException(
        Organization organization,
        OrganizationMember member,
        Guid conversationId,
        Guid anotherConversationId,
        DateTimeOffset createdAt,
        Guid cursorId)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();
        var encoded = codec.Encode(new ConversationMessageCursor(anotherConversationId, createdAt, cursorId));
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        var handler = CreateHandler(organization, member, listingService);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.HandleAsync(
                new GetConversationMessagesCommand(conversationId, null, encoded),
                CancellationToken.None));
        Assert.False(listingService.WasCalled);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownConversation_When_HandleAsync_Then_ThrowsNotFoundException(
        Organization organization,
        OrganizationMember member,
        Guid conversationId)
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(result: null);
        var handler = CreateHandler(organization, member, listingService);

        // When / Then
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(
                new GetConversationMessagesCommand(conversationId, null, null),
                CancellationToken.None));
    }

    [Theory, AutoDomainData]
    public async Task Given_AValidRequest_When_HandleAsync_Then_ReturnsTheMappedResponse(
        Organization organization,
        OrganizationMember member,
        Guid conversationId,
        ConversationMessageResponse messageResponse)
    {
        // Given
        var page = new ConversationMessageListingPage([messageResponse], "next-cursor-value", true);
        var listingService = new RecordingConversationMessageListingService(page);
        var handler = CreateHandler(organization, member, listingService);

        // When
        var response = await handler.HandleAsync(
            new GetConversationMessagesCommand(conversationId, 50, null),
            CancellationToken.None);

        // Then
        Assert.Equal(organization.Id, listingService.ReceivedOrganizationId);
        Assert.Equal(member.Id, listingService.ReceivedOwnerMemberId);
        Assert.Equal(conversationId, listingService.ReceivedConversationId);
        Assert.Equal(50, listingService.ReceivedLimit);
        Assert.Equal(conversationId, response.ConversationId);
        Assert.Same(messageResponse, response.Messages.Single());
        Assert.Equal("next-cursor-value", response.NextCursor);
        Assert.True(response.HasMore);
    }

    private static GetConversationMessagesCommandHandler CreateHandler(
        Organization organization,
        OrganizationMember member,
        IConversationMessageListingService listingService) =>
        new(
            new StubUserContextService(new MessageUserContext(organization, member)),
            listingService,
            new ConversationMessageCursorCodec());

    private sealed class StubUserContextService(MessageUserContext context)
        : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(context);
    }

    private sealed class RecordingConversationMessageListingService(
        ConversationMessageListingPage? result) : IConversationMessageListingService
    {
        public bool WasCalled { get; private set; }

        public Guid ReceivedOrganizationId { get; private set; }

        public Guid ReceivedOwnerMemberId { get; private set; }

        public Guid ReceivedConversationId { get; private set; }

        public int ReceivedLimit { get; private set; }

        public Task<ConversationMessageListingPage?> ListAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            int limit,
            DateTimeOffset? cursorCreatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            ReceivedOrganizationId = organizationId;
            ReceivedOwnerMemberId = ownerMemberId;
            ReceivedConversationId = conversationId;
            ReceivedLimit = limit;
            return Task.FromResult(result);
        }
    }
}
