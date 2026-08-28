using AssistantCore.Service.Application.Services.Conversations;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationPreviewFactoryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_NoContent_When_Create_Then_ReturnsNull(string? content)
    {
        // When
        var result = ConversationPreviewFactory.Create(content, 160);

        // Then
        Assert.Null(result);
    }

    [Fact]
    public void Given_AShortMessage_When_Create_Then_ReturnsItTrimmed()
    {
        // When
        var result = ConversationPreviewFactory.Create("  La commande est en cours  ", 160);

        // Then
        Assert.Equal("La commande est en cours", result);
    }

    [Fact]
    public void Given_AMessageWithNewlinesAndRepeatedSpaces_When_Create_Then_CollapsesToASingleLine()
    {
        // When
        var result = ConversationPreviewFactory.Create("La politique  permet\njusqu'a deux jours", 160);

        // Then
        Assert.Equal("La politique permet jusqu'a deux jours", result);
    }

    [Fact]
    public void Given_AMessageLongerThanTheLimit_When_Create_Then_TruncatesWithEllipsis()
    {
        // Given
        var longMessage = new string('a', 200);

        // When
        var result = ConversationPreviewFactory.Create(longMessage, 160);

        // Then
        Assert.Equal(160, result!.Length);
        Assert.Equal(new string('a', 159) + "…", result);
    }

    [Fact]
    public void Given_AMessageAtExactlyTheLimit_When_Create_Then_ReturnsItUnchanged()
    {
        // Given
        var exactMessage = new string('a', 160);

        // When
        var result = ConversationPreviewFactory.Create(exactMessage, 160);

        // Then
        Assert.Equal(exactMessage, result);
    }
}
