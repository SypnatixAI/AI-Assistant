using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Persistence;
using AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Services.Messages.Connectors.InternalData;
using Microsoft.EntityFrameworkCore;

namespace AssistantCore.Service.Infrastructure.Connectors.InternalData;

public sealed class InternalDataSearchRepository(AssistantCoreDbContext dbContext)
    : IInternalDataSearchRepository
{
    public async Task<IReadOnlyCollection<InternalDataSearchRecord>> SearchAsync(
        InternalDataSearchParameters parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ValidateParameters(parameters);

        var results = new List<InternalDataSearchRecord>();

        if (parameters.Categories.Contains(InternalDataCategory.Conversations))
        {
            results.AddRange(await SearchConversationsAsync(parameters, cancellationToken));
        }

        if (parameters.Categories.Contains(InternalDataCategory.Messages))
        {
            results.AddRange(await SearchMessagesAsync(parameters, cancellationToken));
        }

        return results
            .OrderByDescending(result => result.OccurredAt)
            .Take(parameters.MaximumResults)
            .ToArray();
    }

    private Task<InternalDataSearchRecord[]> SearchConversationsAsync(
        InternalDataSearchParameters parameters,
        CancellationToken cancellationToken) =>
        dbContext.Conversations
            .AsNoTracking()
            .Where(conversation =>
                conversation.OrganizationId == parameters.OrganizationId
                && conversation.OwnerMemberId == parameters.MemberId
                && conversation.Title.Contains(parameters.Query))
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .Take(parameters.MaximumResults)
            .Select(conversation => new InternalDataSearchRecord(
                InternalDataCategory.Conversations,
                conversation.Title,
                conversation.Title,
                conversation.Id.ToString(),
                conversation.UpdatedAt))
            .ToArrayAsync(cancellationToken);

    private Task<InternalDataSearchRecord[]> SearchMessagesAsync(
        InternalDataSearchParameters parameters,
        CancellationToken cancellationToken) =>
        dbContext.Messages
            .AsNoTracking()
            .Where(message =>
                message.Conversation.OrganizationId == parameters.OrganizationId
                && message.Conversation.OwnerMemberId == parameters.MemberId
                && message.ProcessingStatus == MessageProcessingStatus.Completed
                && (message.Content.Contains(parameters.Query)
                    || message.Conversation.Title.Contains(parameters.Query)))
            .OrderByDescending(message => message.UpdatedAt)
            .Take(parameters.MaximumResults)
            .Select(message => new InternalDataSearchRecord(
                InternalDataCategory.Messages,
                message.Conversation.Title,
                message.Content,
                message.Id.ToString(),
                message.UpdatedAt))
            .ToArrayAsync(cancellationToken);

    private static void ValidateParameters(InternalDataSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters.Categories);

        if (parameters.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "The organization identifier is required.",
                nameof(parameters));
        }

        if (parameters.MemberId == Guid.Empty)
        {
            throw new ArgumentException(
                "The member identifier is required.",
                nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(parameters.Query))
        {
            throw new ArgumentException(
                "The search query is required.",
                nameof(parameters));
        }

        if (parameters.MaximumResults <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parameters),
                "The maximum number of results must be greater than zero.");
        }
    }
}
