using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.ListConversations;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Conversations.Pagination;
using AssistantCore.Service.Application.Services.Messages.Authorization;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ListConversationsCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AFullPage_When_HandleAsync_Then_BuildsNextCursorFromTheLastItem(
        Organization organization,
        OrganizationMember member,
        ConversationSummaryResponse firstItem,
        ConversationSummaryResponse lastItem)
    {
        // Given
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([firstItem, lastItem], HasMore: true));
        var handler = CreateHandler(organization, member, listingService);

        // When
        var response = await handler.HandleAsync(
            new ListConversationsCommand(null, null, null),
            CancellationToken.None);

        // Then
        Assert.Equal(organization.Id, listingService.ReceivedOrganizationId);
        Assert.Equal(member.Id, listingService.ReceivedOwnerMemberId);
        Assert.Equal(25, listingService.ReceivedLimit);
        Assert.Null(listingService.ReceivedCursorUpdatedAt);
        Assert.Null(listingService.ReceivedCursorId);
        Assert.Same(firstItem, response.Conversations.First());
        Assert.Same(lastItem, response.Conversations.Last());
        Assert.True(response.HasMore);
        Assert.NotNull(response.NextCursor);

        var codec = new ConversationCursorCodec();
        var decodedCursor = codec.Decode(response.NextCursor);
        Assert.Equal(lastItem.UpdatedAt, decodedCursor!.UpdatedAt);
        Assert.Equal(lastItem.Id, decodedCursor.Id);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheLastPage_When_HandleAsync_Then_ReturnsNoNextCursor(
        Organization organization,
        OrganizationMember member,
        ConversationSummaryResponse item)
    {
        // Given
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([item], HasMore: false));
        var handler = CreateHandler(organization, member, listingService);

        // When
        var response = await handler.HandleAsync(
            new ListConversationsCommand(null, null, null),
            CancellationToken.None);

        // Then
        Assert.False(response.HasMore);
        Assert.Null(response.NextCursor);
    }

    [Theory, AutoDomainData]
    public async Task Given_ALimitAndACursor_When_HandleAsync_Then_PassesDecodedValuesToTheListingService(
        Organization organization,
        OrganizationMember member,
        DateTimeOffset cursorUpdatedAt,
        Guid cursorId)
    {
        // Given
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([], HasMore: false));
        var handler = CreateHandler(organization, member, listingService);
        var codec = new ConversationCursorCodec();
        var encodedCursor = codec.Encode(new ConversationCursor(cursorUpdatedAt, cursorId));

        // When
        await handler.HandleAsync(
            new ListConversationsCommand(50, encodedCursor, null),
            CancellationToken.None);

        // Then
        Assert.Equal(50, listingService.ReceivedLimit);
        Assert.Equal(cursorUpdatedAt, listingService.ReceivedCursorUpdatedAt);
        Assert.Equal(cursorId, listingService.ReceivedCursorId);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnInvalidCursor_When_HandleAsync_Then_ThrowsBadRequestException(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([], HasMore: false));
        var handler = CreateHandler(organization, member, listingService);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.HandleAsync(
                new ListConversationsCommand(null, "not-a-valid-cursor", null),
                CancellationToken.None));
    }

    [Theory, AutoDomainData]
    public async Task Given_NoStatus_When_HandleAsync_Then_ListsActiveConversations(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([], HasMore: false));
        var handler = CreateHandler(organization, member, listingService);

        // When
        await handler.HandleAsync(
            new ListConversationsCommand(null, null, null),
            CancellationToken.None);

        // Then
        Assert.Equal(ConversationStatus.Active, listingService.ReceivedStatus);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheArchivedStatus_When_HandleAsync_Then_ForwardsItToTheListingService(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([], HasMore: false));
        var handler = CreateHandler(organization, member, listingService);

        // When
        await handler.HandleAsync(
            new ListConversationsCommand(null, null, "Archived"),
            CancellationToken.None);

        // Then
        Assert.Equal(ConversationStatus.Archived, listingService.ReceivedStatus);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownStatus_When_HandleAsync_Then_ThrowsBadRequestExceptionBeforeListing(
        Organization organization,
        OrganizationMember member)
    {
        // Given
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([], HasMore: false));
        var handler = CreateHandler(organization, member, listingService);

        // When / Then
        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.HandleAsync(
                new ListConversationsCommand(null, null, "Deleted"),
                CancellationToken.None));
        Assert.Equal(0, listingService.CallCount);
    }

    private static ListConversationsCommandHandler CreateHandler(
        Organization organization,
        OrganizationMember member,
        IConversationListingService listingService) =>
        new(
            new StubUserContextService(new MessageUserContext(organization, member)),
            listingService,
            new ConversationCursorCodec());

    private sealed class StubUserContextService(MessageUserContext context)
        : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(context);
    }

    private sealed class RecordingConversationListingService(ConversationListingPage page)
        : IConversationListingService
    {
        public Guid ReceivedOrganizationId { get; private set; }

        public Guid ReceivedOwnerMemberId { get; private set; }

        public int ReceivedLimit { get; private set; }

        public ConversationStatus ReceivedStatus { get; private set; }

        public int CallCount { get; private set; }

        public DateTimeOffset? ReceivedCursorUpdatedAt { get; private set; }

        public Guid? ReceivedCursorId { get; private set; }

        public Task<ConversationListingPage> ListAsync(
            Guid organizationId,
            Guid ownerMemberId,
            ConversationStatus status,
            int limit,
            DateTimeOffset? cursorUpdatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ReceivedOrganizationId = organizationId;
            ReceivedOwnerMemberId = ownerMemberId;
            ReceivedLimit = limit;
            ReceivedStatus = status;
            ReceivedCursorUpdatedAt = cursorUpdatedAt;
            ReceivedCursorId = cursorId;
            return Task.FromResult(page);
        }
    }
}
