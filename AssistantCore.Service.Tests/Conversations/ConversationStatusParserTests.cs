using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Conversations;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationStatusParserTests
{
    [Theory]
    [InlineData("Active", ConversationStatus.Active)]
    [InlineData("Archived", ConversationStatus.Archived)]
    public void Given_AKnownStatus_When_Parse_Then_ReturnsTheDomainValue(
        string status,
        ConversationStatus expected)
    {
        // Given
        // le statut arrive tel quel du frontend

        // When
        var result = ConversationStatusParser.Parse(status);

        // Then
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Deleted")]
    [InlineData("active")]
    [InlineData("ARCHIVED")]
    [InlineData("Archive")]
    public void Given_AnUnknownStatus_When_Parse_Then_ThrowsBadRequestException(string status)
    {
        // Given
        // la valeur n'appartient pas au domaine autorise

        // When / Then
        Assert.Throws<BadRequestException>(() => ConversationStatusParser.Parse(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_NoStatus_When_ParseOrDefault_Then_ReturnsActive(string? status)
    {
        // Given
        // la lecture ne demande aucun statut particulier

        // When
        var result = ConversationStatusParser.ParseOrDefault(status);

        // Then
        Assert.Equal(ConversationStatus.Active, result);
        Assert.Equal(ConversationStatusParser.DefaultListingStatus, result);
    }

    [Fact]
    public void Given_TheArchivedStatus_When_ParseOrDefault_Then_ReturnsArchived()
    {
        // Given
        const string status = "Archived";

        // When
        var result = ConversationStatusParser.ParseOrDefault(status);

        // Then
        Assert.Equal(ConversationStatus.Archived, result);
    }

    [Fact]
    public void Given_AnUnknownStatus_When_ParseOrDefault_Then_ThrowsBadRequestException()
    {
        // Given
        const string status = "Supprimee";

        // When / Then
        Assert.Throws<BadRequestException>(() => ConversationStatusParser.ParseOrDefault(status));
    }
}
