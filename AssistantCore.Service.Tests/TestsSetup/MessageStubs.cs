using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;

namespace AssistantCore.Service.Tests;

internal sealed class RecordingMessageProcessingLifecycleService
    : IMessageProcessingLifecycleService
{
    public int StartCallCount { get; private set; }

    public Task<StartedMessageProcessing> StartAsync(
        Guid? conversationId,
        string message,
        Organization organization,
        OrganizationMember member,
        CancellationToken cancellationToken)
    {
        StartCallCount++;
        throw new InvalidOperationException("Message processing was not expected to start.");
    }

    public Task MarkAsInProgressAsync(
        StartedMessageProcessing processing,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CompletedMessageProcessing> CompleteAsync(
        StartedMessageProcessing processing,
        MessageOrchestrationResult result,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task FailAsync(
        StartedMessageProcessing processing,
        MessageProcessingFailure failure,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
