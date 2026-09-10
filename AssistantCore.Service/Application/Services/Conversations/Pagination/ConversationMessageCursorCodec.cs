using System.Text;
using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Conversations.Pagination;

public sealed class ConversationMessageCursorCodec : IConversationMessageCursorCodec
{
    public string Encode(ConversationMessageCursor cursor)
    {
        var payload = new CursorPayload(cursor.ConversationId, cursor.CreatedAt, cursor.Id);
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public ConversationMessageCursor? Decode(string? cursor, Guid conversationId)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);

            if (payload is null || payload.Id == Guid.Empty || payload.ConversationId == Guid.Empty)
            {
                throw new BadRequestException("cursor is invalid.");
            }

            if (payload.ConversationId != conversationId)
            {
                throw new BadRequestException("cursor does not match the requested conversation.");
            }

            return new ConversationMessageCursor(payload.ConversationId, payload.CreatedAt, payload.Id);
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or DecoderFallbackException)
        {
            throw new BadRequestException("cursor is invalid.");
        }
    }

    private sealed record CursorPayload(Guid ConversationId, DateTimeOffset CreatedAt, Guid Id);
}
