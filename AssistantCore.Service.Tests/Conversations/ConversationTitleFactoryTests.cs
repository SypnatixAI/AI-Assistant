using AssistantCore.Service.Application.Services.Conversations;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationTitleFactoryTests
{
    [Fact]
    public void Given_AShortMessage_When_CreateFromFirstMessage_Then_ReturnsItTrimmed()
    {
        // When
        var result = ConversationTitleFactory.CreateFromFirstMessage("  Politique de teletravail  ");

        // Then
        Assert.Equal("Politique de teletravail", result);
    }

    [Fact]
    public void Given_AMessageWithNewlinesAndRepeatedSpaces_When_CreateFromFirstMessage_Then_CollapsesToASingleLine()
    {
        // When
        var result = ConversationTitleFactory.CreateFromFirstMessage("Quel   est\nle statut\r\nde la commande 4587 ?");

        // Then
        Assert.Equal("Quel est le statut de la commande 4587 ?", result);
    }

    [Fact]
    public void Given_AMessageLongerThanTheLimit_When_CreateFromFirstMessage_Then_TruncatesWithEllipsis()
    {
        // Given
        var longMessage = new string('a', 250);

        // When
        var result = ConversationTitleFactory.CreateFromFirstMessage(longMessage);

        // Then
        Assert.Equal(200, result.Length);
        Assert.Equal(new string('a', 199) + "…", result);
    }

    [Fact]
    public void Given_AMessageAtExactlyTheLimit_When_CreateFromFirstMessage_Then_ReturnsItUnchanged()
    {
        // Given
        var exactMessage = new string('a', 200);

        // When
        var result = ConversationTitleFactory.CreateFromFirstMessage(exactMessage);

        // Then
        Assert.Equal(exactMessage, result);
    }
}
