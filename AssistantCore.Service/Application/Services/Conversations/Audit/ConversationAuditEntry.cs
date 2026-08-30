namespace AssistantCore.Service.Application.Services.Conversations.Audit;

/// <summary>
/// Trace d'une modification reelle du cycle de vie d'une conversation. L'entree ne
/// transporte aucun contenu utilisateur : ni titre, ni message, ni source.
/// </summary>
public sealed record ConversationAuditEntry(
    Guid OrganizationId,
    Guid MemberId,
    Guid ConversationId,
    ConversationAuditAction Action,
    DateTimeOffset OccurredAt);
