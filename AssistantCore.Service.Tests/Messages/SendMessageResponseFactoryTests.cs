using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Responses;

namespace AssistantCore.Service.Tests.Messages;

public sealed class SendMessageResponseFactoryTests
{
    [Theory, AutoDomainData]
    public void Given_ACompletedProcessing_When_Create_Then_MapsPersistedResponseAndSources(
        StartedMessageProcessing processing,
        CompletedMessageProcessing completedProcessing,
        DateTimeOffset occurredAt)
    {
        // Given
        var evidence = new RetrievedEvidence(
            "evidence-1",
            "SharePoint",
            "Sales report",
            "Sales increased.",
            "report-1",
            "https://example.test/report-1",
            occurredAt);
        var orchestrationResult = new MessageOrchestrationResult(
            "Sales increased.",
            "gpt-5.6-luna",
            [evidence],
            ["One source was unavailable."],
            OrchestrationExecutionUsage.Empty);
        var factory = new SendMessageResponseFactory();

        // When
        var response = factory.Create(
            processing,
            orchestrationResult,
            completedProcessing);

        // Then
        Assert.Equal(processing.ConversationId, response.ConversationId);
        Assert.Equal(completedProcessing.AssistantMessageId, response.MessageId);
        Assert.Equal(orchestrationResult.Answer, response.Answer);
        Assert.Equal(orchestrationResult.ModelName, response.Model);
        Assert.Equal(orchestrationResult.Warnings, response.Warnings);
        Assert.Equal(completedProcessing.CreatedAt, response.CreatedAt);
        var source = Assert.Single(response.Sources);
        Assert.Equal(evidence.SourceType, source.Type);
        Assert.Equal(evidence.Title, source.Title);
        Assert.Equal(evidence.Url, source.Url);
        Assert.Equal(evidence.Reference, source.Reference);
    }
}
