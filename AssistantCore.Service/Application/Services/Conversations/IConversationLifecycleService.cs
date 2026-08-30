using AssistantCore.Service.Application.Models.Conversations;

namespace AssistantCore.Service.Application.Services.Conversations;

public interface IConversationLifecycleService
{
    Task<ConversationResponse> UpdateAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        string? title,
        string? status,
        int? expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Supprime logiquement une conversation et programme sa purge.
    /// Retourne <c>true</c> lorsque la conversation etait deja supprimee, ce qui permet
    /// a l'appelant de rester idempotent sans distinguer les deux cas pour le client.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid organizationId,
        Guid ownerMemberId,
        Guid conversationId,
        CancellationToken cancellationToken);
}
