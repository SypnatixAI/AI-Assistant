namespace AssistantCore.Service.Application.Services.Conversations.Pagination;

public interface IConversationMessageCursorCodec
{
    string Encode(ConversationMessageCursor cursor);

    /// <summary>
    /// Decode un curseur opaque. Retourne null lorsque aucun curseur n'est fourni
    /// (premiere page). Leve <see cref="AssistantCore.Service.Application.Exceptions.BadRequestException"/>
    /// lorsque le curseur fourni n'est pas valide, ou lorsqu'il a ete construit
    /// pour une conversation differente de <paramref name="conversationId"/>.
    /// </summary>
    ConversationMessageCursor? Decode(string? cursor, Guid conversationId);
}
