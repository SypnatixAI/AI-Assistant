using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Conversations;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Conversations;

public sealed class ConversationListingService(
    IConversationRepository conversationRepository,
    IOptions<ConversationListingOptions> options) : IConversationListingService
{
    public async Task<ConversationListingPage> ListAsync(
        Guid organizationId,
        Guid ownerMemberId,
        int limit,
        DateTimeOffset? cursorUpdatedAt,
        Guid? cursorId,
        CancellationToken cancellationToken)
    {
        var page = await conversationRepository.ListConversationsAsync(
            organizationId,
            ownerMemberId,
            limit,
            cursorUpdatedAt,
            cursorId,
            cancellationToken);

        var items = page.Items
            .Select(item => new ConversationSummaryResponse(
                item.Id,
                item.Title,
                item.CreatedAt,
                item.UpdatedAt,
                ConversationPreviewFactory.Create(
                    item.LastMessageContent,
                    options.Value.MaximumPreviewLength)))
            .ToList();

        return new ConversationListingPage(items, page.HasMore);
    }
}
