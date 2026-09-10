using System.Text;
using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Conversations.Pagination;

public sealed class ConversationCursorCodec : IConversationCursorCodec
{
    public string Encode(ConversationCursor cursor)
    {
        var payload = new CursorPayload(cursor.UpdatedAt, cursor.Id);
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public ConversationCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var payload = JsonSerializer.Deserialize<CursorPayload>(json);

            if (payload is null || payload.Id == Guid.Empty)
            {
                throw new BadRequestException("cursor is invalid.");
            }

            return new ConversationCursor(payload.UpdatedAt, payload.Id);
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or DecoderFallbackException)
        {
            throw new BadRequestException("cursor is invalid.");
        }
    }

    private sealed record CursorPayload(DateTimeOffset UpdatedAt, Guid Id);
}
