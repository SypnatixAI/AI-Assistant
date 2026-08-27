using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Conversations.Pagination;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationCursorCodecTests
{
    [Fact]
    public void Given_NoCursor_When_Decode_Then_ReturnsNull()
    {
        // Given
        var codec = new ConversationCursorCodec();

        // When
        var result = codec.Decode(null);

        // Then
        Assert.Null(result);
    }

    [Fact]
    public void Given_AnEmptyCursor_When_Decode_Then_ReturnsNull()
    {
        // Given
        var codec = new ConversationCursorCodec();

        // When
        var result = codec.Decode("   ");

        // Then
        Assert.Null(result);
    }

    [Theory, AutoDomainData]
    public void Given_AnEncodedCursor_When_Decode_Then_RoundTripsToTheSameValue(
        DateTimeOffset updatedAt,
        Guid id)
    {
        // Given
        var codec = new ConversationCursorCodec();
        var cursor = new ConversationCursor(updatedAt, id);

        // When
        var encoded = codec.Encode(cursor);
        var decoded = codec.Decode(encoded);

        // Then
        Assert.Equal(cursor, decoded);
    }

    [Fact]
    public void Given_AnInvalidBase64Cursor_When_Decode_Then_ThrowsBadRequestException()
    {
        // Given
        var codec = new ConversationCursorCodec();

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode("not-valid-base64!!!"));
    }

    [Fact]
    public void Given_ATruncatedCursor_When_Decode_Then_ThrowsBadRequestException()
    {
        // Given
        var codec = new ConversationCursorCodec();
        var validCursor = codec.Encode(new ConversationCursor(DateTimeOffset.UtcNow, Guid.NewGuid()));
        var truncated = validCursor[..(validCursor.Length / 2)];

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode(truncated));
    }

    [Fact]
    public void Given_AWellFormedButUnrelatedJsonCursor_When_Decode_Then_ThrowsBadRequestException()
    {
        // Given
        var codec = new ConversationCursorCodec();
        var unrelatedJson = Convert.ToBase64String("{\"foo\":\"bar\"}"u8.ToArray());

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode(unrelatedJson));
    }

    [Fact]
    public void Given_ABase64EncodedGarbageString_When_Decode_Then_ThrowsBadRequestException()
    {
        // Given
        var codec = new ConversationCursorCodec();
        var garbage = Convert.ToBase64String("this is not json"u8.ToArray());

        // When / Then
        Assert.Throws<BadRequestException>(() => codec.Decode(garbage));
    }
}
