using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Conversations.Audit;

/// <summary>
/// Implementation transitoire : les actions sont tracees dans les logs techniques en
/// attendant le journal administratif persistant. Les points d'appel etant deja en place,
/// il suffira de substituer cette implementation sans toucher aux services appelants.
/// </summary>
public sealed class LoggingConversationAuditWriter(
    ILogger<LoggingConversationAuditWriter> logger) : IConversationAuditWriter
{
    public Task RecordAsync(ConversationAuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        logger.LogInformation(
            "Conversation lifecycle action {Action} on conversation {ConversationId} by member {MemberId} of organization {OrganizationId} at {OccurredAt}.",
            entry.Action,
            entry.ConversationId,
            entry.MemberId,
            entry.OrganizationId,
            entry.OccurredAt);

        return Task.CompletedTask;
    }
}
