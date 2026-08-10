using System.ComponentModel.DataAnnotations;
using AssistantCore.Service.Application.Commands.SendMessage.Models;

namespace AssistantCore.Service.Tests.Messages;

public sealed class SendMessageRequestTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("gpt")]
    [InlineData("claude")]
    public void Given_AValidPayload_When_TryValidateObject_Then_ReturnsNoValidationErrors(
        string? model)
    {
        // Given
        var request = new SendMessageRequest(
            Guid.NewGuid(),
            "Quelle est l'evolution des ventes?",
            model);
        var validationResults = new List<ValidationResult>();

        // When
        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        // Then
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_AMissingMessage_When_TryValidateObject_Then_ReturnsValidationError(
        string? message)
    {
        // Given
        var request = new SendMessageRequest(null, message!, null);
        var validationResults = new List<ValidationResult>();

        // When
        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        // Then
        Assert.False(isValid);
        Assert.Contains(validationResults, result =>
            result.MemberNames.Contains(nameof(SendMessageRequest.Message)));
    }

}
