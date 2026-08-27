namespace AssistantCore.Service.Application.Services.Conversations.Pagination;

public interface IConversationCursorCodec
{
    string Encode(ConversationCursor cursor);

    /// <summary>
    /// Decode un curseur opaque. Retourne null lorsque aucun curseur n'est fourni
    /// (premiere page). Leve <see cref="AssistantCore.Service.Application.Exceptions.BadRequestException"/>
    /// lorsque le curseur fourni n'est pas valide.
    /// </summary>
    ConversationCursor? Decode(string? cursor);
}
