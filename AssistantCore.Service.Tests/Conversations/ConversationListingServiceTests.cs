using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Conversations;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationListingServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_ItemsWithMessages_When_ListAsync_Then_MapsAndTruncatesThePreview(
        Guid organizationId,
        Guid ownerMemberId,
        Guid firstId,
        Guid secondId,
        DateTimeOffset updatedAt)
    {
        // Given
        var items = new[]
        {
            new ConversationListItem(firstId, "Premiere conversation", updatedAt, updatedAt, "  Bonjour   le monde  "),
            new ConversationListItem(secondId, "Deuxieme conversation", updatedAt, updatedAt, null)
        };
        var repository = new RecordingConversationRepository(
            new ConversationListPage(items, HasMore: true));
        var service = new ConversationListingService(
            repository,
            Options.Create(new ConversationListingOptions { MaximumPreviewLength = 160 }));

        // When
        var result = await service.ListAsync(
            organizationId,
            ownerMemberId,
            25,
            null,
            null,
            CancellationToken.None);

        // Then
        Assert.Equal(organizationId, repository.ReceivedOrganizationId);
        Assert.Equal(ownerMemberId, repository.ReceivedOwnerMemberId);
        Assert.Equal(25, repository.ReceivedLimit);
        Assert.True(result.HasMore);
        Assert.Collection(
            result.Items,
            summary =>
            {
                Assert.Equal(firstId, summary.Id);
                Assert.Equal("Premiere conversation", summary.Title);
                Assert.Equal("Bonjour le monde", summary.LastMessagePreview);
            },
            summary =>
            {
                Assert.Equal(secondId, summary.Id);
                Assert.Null(summary.LastMessagePreview);
            });
    }

    [Theory, AutoDomainData]
    public async Task Given_APreviewLongerThanTheConfiguredLimit_When_ListAsync_Then_TruncatesWithEllipsis(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        DateTimeOffset updatedAt)
    {
        // Given
        var longMessage = new string('a', 200);
        var items = new[]
        {
            new ConversationListItem(conversationId, "Titre", updatedAt, updatedAt, longMessage)
        };
        var repository = new RecordingConversationRepository(
            new ConversationListPage(items, HasMore: false));
        var service = new ConversationListingService(
            repository,
            Options.Create(new ConversationListingOptions { MaximumPreviewLength = 160 }));

        // When
        var result = await service.ListAsync(
            organizationId,
            ownerMemberId,
            25,
            null,
            null,
            CancellationToken.None);

        // Then
        var preview = result.Items.Single().LastMessagePreview;
        Assert.Equal(160, preview!.Length);
        Assert.Equal(new string('a', 159) + "…", preview);
    }

    private sealed class RecordingConversationRepository(ConversationListPage page)
        : IConversationRepository
    {
        public Task<ConversationUpdateResult> UpdateConversationAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            int? expectedVersion,
            string? title,
            ConversationStatus? status,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ConversationDeleteStatus> SoftDeleteConversationAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            DateTimeOffset deletedAt,
            DateTimeOffset purgeAfter,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Guid ReceivedOrganizationId { get; private set; }

        public Guid ReceivedOwnerMemberId { get; private set; }

        public int ReceivedLimit { get; private set; }

        public Task<ConversationListPage> ListConversationsAsync(
            Guid organizationId,
            Guid ownerMemberId,
            int limit,
            DateTimeOffset? cursorUpdatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken = default)
        {
            ReceivedOrganizationId = organizationId;
            ReceivedOwnerMemberId = ownerMemberId;
            ReceivedLimit = limit;
            return Task.FromResult(page);
        }

        public Task<(Conversation Conversation, Message UserMessage)> CreateConversationWithFirstMessageAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Conversation conversation,
            Message userMessage,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ConversationMessagePage> ListMessagesAsync(
            Guid conversationId,
            int limit,
            DateTimeOffset? cursorCreatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ConversationMessageItem>> GetConversationHistoryAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> UpdateConversationContextSummaryAsync(
            Guid organizationId, Guid ownerMemberId, Guid conversationId, string summary,
            DateTimeOffset updatedAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Conversation?> FindConversationAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Message?> AddUserMessageAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Message userMessage,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> UpdateMessageProcessingStatusAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Guid messageId,
            MessageProcessingStatus status,
            DateTimeOffset updatedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Message?> CompleteMessageWithAssistantResponseAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Guid userMessageId,
            Message assistantMessage,
            IReadOnlyCollection<MessageSource> sources,
            IReadOnlyCollection<MessageWarning> warnings,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> FailMessageProcessingAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            Guid userMessageId,
            MessageProcessingStatus failureStatus,
            string errorCode,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
