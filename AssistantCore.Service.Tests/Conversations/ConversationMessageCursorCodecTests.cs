using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Conversations.Pagination;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationMessageCursorCodecTests
{
    [Theory, AutoDomainData]
    public void Given_NoCursor_When_Decode_Then_ReturnsNull(Guid conversationId)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();

        // When
        var result = codec.Decode(null, conversationId);

        // Then
        Assert.Null(result);
    }

    [Theory, AutoDomainData]
    public void Given_AnEmptyCursor_When_Decode_Then_ReturnsNull(Guid conversationId)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();

        // When
        var result = codec.Decode("   ", conversationId);

        // Then
        Assert.Null(result);
    }

    [Theory, AutoDomainData]
    public void Given_AnEncodedCursor_When_DecodeForTheSameConversation_Then_RoundTripsToTheSameValue(
        Guid conversationId,
        DateTimeOffset createdAt,
        Guid id)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();
        var cursor = new ConversationMessageCursor(conversationId, createdAt, id);

        // When
        var encoded = codec.Encode(cursor);
        var decoded = codec.Decode(encoded, conversationId);

        // Then
        Assert.Equal(cursor, decoded);
    }

    [Theory, AutoDomainData]
    public void Given_ACursorFromAnotherConversation_When_Decode_Then_ThrowsBadRequestException(
        Guid conversationId,
        Guid anotherConversationId,
        DateTimeOffset createdAt,
        Guid id)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();
        var encoded = codec.Encode(new ConversationMessageCursor(conversationId, createdAt, id));

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode(encoded, anotherConversationId));
    }

    [Theory, AutoDomainData]
    public void Given_AnInvalidBase64Cursor_When_Decode_Then_ThrowsBadRequestException(Guid conversationId)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode("not-valid-base64!!!", conversationId));
    }

    [Theory, AutoDomainData]
    public void Given_ATruncatedCursor_When_Decode_Then_ThrowsBadRequestException(Guid conversationId)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();
        var validCursor = codec.Encode(
            new ConversationMessageCursor(conversationId, DateTimeOffset.UtcNow, Guid.NewGuid()));
        var truncated = validCursor[..(validCursor.Length / 2)];

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode(truncated, conversationId));
    }

    [Theory, AutoDomainData]
    public void Given_AWellFormedButUnrelatedJsonCursor_When_Decode_Then_ThrowsBadRequestException(
        Guid conversationId)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();
        var unrelatedJson = Convert.ToBase64String("{\"foo\":\"bar\"}"u8.ToArray());

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode(unrelatedJson, conversationId));
    }

    [Theory, AutoDomainData]
    public void Given_ABase64EncodedGarbageString_When_Decode_Then_ThrowsBadRequestException(
        Guid conversationId)
    {
        // Given
        var codec = new ConversationMessageCursorCodec();
        var garbage = Convert.ToBase64String("this is not json"u8.ToArray());

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode(garbage, conversationId));
    }
}
