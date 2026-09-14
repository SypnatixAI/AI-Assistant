using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Repository.Repositories;

public sealed class MessageCompletionRepository(
    AssistantCoreDbContext dbContext,
    IConversationRepository conversationRepository) : IMessageCompletionRepository
{
    public async Task<Message?> CompleteWithUsageAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        Guid userMessageId,
        Message assistantMessage,
        IReadOnlyCollection<MessageSource> sources,
        IReadOnlyCollection<MessageWarning> warnings,
        TokenConsumption consumption,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default)
    {
        if (consumption.OrganizationId != organizationId)
        {
            throw new ArgumentException(
                "The token consumption organization does not match the authenticated organization.",
                nameof(consumption));
        }

        if (consumption.AssistantMessageId != assistantMessage.Id)
        {
            throw new ArgumentException(
                "The token consumption must reference the assistant message being completed.",
                nameof(consumption));
        }

        dbContext.TokenConsumptions.Add(consumption);

        var completedMessage = await conversationRepository.CompleteMessageWithAssistantResponseAsync(
            organizationId,
            ownerMemberId,
            conversationId,
            userMessageId,
            assistantMessage,
            sources,
            warnings,
            completedAt,
            cancellationToken);

        if (completedMessage is null)
        {
            dbContext.Entry(consumption).State = EntityState.Detached;
        }

        return completedMessage;
    }
}
