namespace AssistantCore.Service.Application.Services.Conversations.Audit;

public interface IConversationAuditWriter
{
    Task RecordAsync(ConversationAuditEntry entry, CancellationToken cancellationToken);
}
