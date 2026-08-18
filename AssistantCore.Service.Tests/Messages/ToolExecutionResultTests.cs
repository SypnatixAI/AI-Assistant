using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class ToolExecutionResultTests
{
    [Theory, AutoDomainData]
    public void Given_Evidence_When_Succeeded_Then_ReturnsImmutableSuccessfulResult(
        Guid callId,
        RetrievedEvidence evidence)
    {
        // Given
        var sourceEvidence = new List<RetrievedEvidence> { evidence };

        // When
        var result = ToolExecutionResult.Succeeded(callId.ToString(), sourceEvidence);
        sourceEvidence.Clear();

        // Then
        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Equal([evidence], result.Evidence);
        Assert.Empty(result.Warnings);
        Assert.Null(result.ErrorCode);
    }

    [Theory, AutoDomainData]
    public void Given_EvidenceAndWarnings_When_PartiallySucceeded_Then_ReturnsPartialResult(
        Guid callId,
        RetrievedEvidence evidence,
        string warning)
    {
        // Given
        var evidenceItems = new[] { evidence };
        var warnings = new[] { warning };

        // When
        var result = ToolExecutionResult.PartiallySucceeded(
            callId.ToString(),
            evidenceItems,
            warnings);

        // Then
        Assert.Equal(ToolExecutionStatus.PartialSuccess, result.Status);
        Assert.Equal(evidenceItems, result.Evidence);
        Assert.Equal(warnings, result.Warnings);
        Assert.Null(result.ErrorCode);
    }

    [Theory, AutoDomainData]
    public void Given_AnErrorCode_When_Failed_Then_ReturnsFailureWithoutEvidence(
        Guid callId,
        string warning)
    {
        // Given
        var warnings = new[] { warning };

        // When
        var result = ToolExecutionResult.Failed(
            callId.ToString(),
            ToolExecutionErrorCodes.ExecutorNotFound,
            warnings);

        // Then
        Assert.Equal(ToolExecutionStatus.Failed, result.Status);
        Assert.Empty(result.Evidence);
        Assert.Equal(warnings, result.Warnings);
        Assert.Equal(ToolExecutionErrorCodes.ExecutorNotFound, result.ErrorCode);
    }

    [Theory, AutoDomainData]
    public void Given_NoEvidence_When_PartiallySucceeded_Then_ThrowsArgumentException(
        Guid callId,
        string warning)
    {
        // Given
        var warnings = new[] { warning };

        // When
        var exception = Record.Exception(() =>
            ToolExecutionResult.PartiallySucceeded(
                callId.ToString(),
                [],
                warnings));

        // Then
        Assert.IsType<ArgumentException>(exception);
    }

    [Theory]
    [InlineAutoDomainData("")]
    [InlineAutoDomainData(" ")]
    public void Given_AnInvalidErrorCode_When_Failed_Then_ThrowsArgumentException(
        string errorCode,
        Guid callId)
    {
        // Given

        // When
        var exception = Record.Exception(() =>
            ToolExecutionResult.Failed(callId.ToString(), errorCode));

        // Then
        Assert.IsType<ArgumentException>(exception);
    }
}
