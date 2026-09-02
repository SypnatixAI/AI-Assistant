using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.AiModels;
using AssistantCore.Service.Application.Services.Messages.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistantCore.Service.Tests.Messages;

public sealed class ConversationMemorySummaryServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_AProviderFailure_When_CreateAsync_Then_ReturnsNoAiSummary(
        SelectedAiModel model)
    {
        // Given
        var provider = new ThrowingAiModelProvider(model.Provider);
        var service = new ConversationMemorySummaryService(
            [provider],
            NullLogger<ConversationMemorySummaryService>.Instance);

        // When
        var result = await service.CreateAsync(
            model,
            [],
            "What was decided?",
            "The rollout is approved.",
            CancellationToken.None);

        // Then
        Assert.Null(result);
    }

    private sealed class ThrowingAiModelProvider(string providerName) : IAiModelProvider
    {
        public string ProviderName => providerName;

        public Task<AiModelResponse> GetNextActionAsync(
            AiModelRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiModelResponse> GetNextActionStreamingAsync(
            AiModelRequest request,
            Func<string, CancellationToken, ValueTask> onTextDelta,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> CreateConversationSummaryAsync(
            AiConversationSummaryRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Provider unavailable.");
    }
}
