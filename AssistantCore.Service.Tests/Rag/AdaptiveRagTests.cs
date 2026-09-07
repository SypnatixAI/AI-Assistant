using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Rag;
using AssistantCore.Service.Application.Services.Messages.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Rag;

public sealed class AdaptiveRagTests
{
    [Theory]
    [InlineAutoDomainData(3.5, 0.65, true)]
    [InlineAutoDomainData(1.0, 0.65, false)]
    [InlineAutoDomainData(3.5, 0.95, false)]
    public async Task Given_SemanticScoreAndThreshold_When_EvaluateAsync_Then_UsesConfiguredConfidence(double score, double threshold, bool expected)
    {
        // Given
        var evaluator = new RetrievalQualityEvaluator(Options.Create(new RagOptions
        { CorrectiveRag = new() { RetrievalConfidenceThreshold = threshold } }));
        // When
        var result = await evaluator.EvaluateAsync("policy", [new("p", "Policy text", "document", score)]);
        // Then
        Assert.Equal(expected, result.IsSufficient);
    }

    [Theory]
    [InlineAutoDomainData("La limite est 5000.", true)]
    [InlineAutoDomainData("La limite est 6000.", false)]
    [InlineAutoDomainData("La limite est 5000. Le budget est illimité.", false)]
    [InlineAutoDomainData("La limite est 5000 et le budget est illimité.", false)]
    public async Task Given_Claims_When_EvaluateAsync_Then_RequiresSupportForEverySentence(string answer, bool expected)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        // When
        var result = await evaluator.EvaluateAsync(answer, [new("policy", "La limite est 5000.", "policy")]);
        // Then
        Assert.Equal(expected, result.Passed);
    }

    [Theory, AutoDomainData]
    public async Task Given_StrongRetrieval_When_RetrieveAsync_Then_DoesNotCorrect(Guid organizationId)
    {
        // Given
        var context = Context(organizationId);
        var calls = 0;
        var service = Service();
        // When
        var result = await service.RetrieveAsync(Parameters(organizationId), context, (_, token) =>
        {
            token.ThrowIfCancellationRequested(); calls++;
            return Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>([Record(3.5)]);
        }, CancellationToken.None);
        // Then
        Assert.Single(result);
        Assert.Equal(1, calls);
        Assert.Equal(0, context.RagStatus!.CorrectionAttempts);
    }

    [Theory, AutoDomainData]
    public async Task Given_WeakRetrieval_When_RetrieveAsync_Then_CorrectsOnceWithIdenticalSecurityAndDates(Guid organizationId)
    {
        // Given
        var parameters = Parameters(organizationId);
        var context = Context(organizationId);
        var attempts = new List<Microsoft365SearchParameters>();
        // When
        await Service().RetrieveAsync(parameters, context, (request, token) =>
        {
            Assert.True(token.CanBeCanceled || attempts.Count == 0);
            attempts.Add(request);
            return Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>([Record(0.5)]);
        }, CancellationToken.None);
        // Then
        Assert.Equal(2, attempts.Count);
        Assert.True(attempts[1].TextOnly);
        Assert.Same(parameters.SecurityContext, attempts[1].SecurityContext);
        Assert.Equal(parameters.DateFrom, attempts[1].DateFrom);
        Assert.Equal(parameters.DateTo, attempts[1].DateTo);
        Assert.Same(parameters.SourceTypes, attempts[1].SourceTypes);
        Assert.Equal(1, context.RagStatus!.CorrectionAttempts);
        Assert.Equal(0, context.Budget!.Usage.ToolCallCount);
        Assert.Equal(0.001m, context.Budget.Usage.EstimatedCost);
    }

    [Theory, AutoDomainData]
    public async Task Given_ExhaustedBudget_When_RetrieveAsync_Then_DoesNotRunCorrection(Guid organizationId)
    {
        // Given
        var context = Context(organizationId, 0m);
        var calls = 0;
        // When
        await Service().RetrieveAsync(Parameters(organizationId), context, (_, _) =>
        {
            calls++;
            return Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>([Record(0.5)]);
        }, CancellationToken.None);
        // Then
        Assert.Equal(1, calls);
    }

    [Theory, AutoDomainData]
    public async Task Given_GraderFailure_When_RetrieveAsync_Then_KeepsOriginalEvidence(Guid organizationId)
    {
        // Given
        var original = new[] { Record(0.5) };
        // When
        var result = await Service(new FailingGrader()).RetrieveAsync(Parameters(organizationId), Context(organizationId),
            (_, _) => Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>(original), CancellationToken.None);
        // Then
        Assert.Same(original, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_CancellationDuringCorrection_When_RetrieveAsync_Then_PropagatesCancellation(Guid organizationId)
    {
        // Given
        using var cancellation = new CancellationTokenSource();
        // When
        var action = () => Service().RetrieveAsync(Parameters(organizationId), Context(organizationId), (p, token) =>
        {
            if (p.TextOnly) { cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
            return Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>([Record(0.5)]);
        }, cancellation.Token);
        // Then
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }

    [Theory]
    [InlineAutoDomainData(false, 0.2, 0)]
    [InlineAutoDomainData(true, 0.2, 1)]
    [InlineAutoDomainData(true, 0.9, 0)]
    public async Task Given_RerankingPolicy_When_RerankAsync_Then_InvokesDedicatedOnlyWhenNeeded(bool enabled, double confidence, int expectedCalls, Guid organizationId)
    {
        // Given
        var dedicated = new RecordingReranker();
        var settings = Options.Create(new RagOptions { Reranking = new() { DedicatedCrossEncoderEnabled = enabled } });
        var service = new AdaptiveRagReranker(new SemanticOnlyRagReranker(), settings, TimeProvider.System, dedicated);
        IReadOnlyList<RagPassage> passages = [new("p", "Policy", "p", 3.5)];
        // When
        var result = await service.RerankAsync("policy", passages, new(confidence >= 0.65, confidence, null), Context(organizationId), CancellationToken.None);
        // Then
        Assert.Equal(expectedCalls, dedicated.Calls);
        Assert.Equal(passages, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoOpReranker_When_RerankAsync_Then_PreservesOrderingAndScores(Guid _)
    {
        // Given
        IReadOnlyList<RagPassage> passages = [new("b", "B", "b", 3), new("a", "A", "a", 2)];
        // When
        var result = await new SemanticOnlyRagReranker().RerankAsync("query", passages);
        // Then
        Assert.Same(passages, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_RerankerInventsPassage_When_RerankAsync_Then_RejectsForeignEvidence(Guid organizationId)
    {
        // Given
        var settings = Options.Create(new RagOptions { Reranking = new() { DedicatedCrossEncoderEnabled = true } });
        var service = new AdaptiveRagReranker(new SemanticOnlyRagReranker(), settings, TimeProvider.System, new ForeignReranker());
        // When
        var action = () => service.RerankAsync("query", [new("authorized", "text", "document")],
            new(false, 0, null), Context(organizationId), CancellationToken.None);
        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Theory, AutoDomainData]
    public async Task Given_MissingScoresAndEmptyContent_When_EvaluateAsync_Then_DoesNotAssumeRelevance(Guid _)
    {
        // Given
        var grader = new RetrievalQualityEvaluator(Options.Create(new RagOptions()));
        // When
        var result = await grader.EvaluateAsync("query", [new("a", "", "a", 4), new("b", "Text", "b")]);
        // Then
        Assert.False(result.IsSufficient);
        Assert.Equal(0, result.Confidence);
    }

    private sealed class ForeignReranker : IDedicatedRagReranker
    {
        public decimal EstimateCost(IReadOnlyList<RagPassage> passages) => 0;
        public Task<IReadOnlyList<RagPassage>> RerankAsync(string query, IReadOnlyList<RagPassage> passages, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RagPassage>>([new("foreign", "private content", "foreign")]);
    }

    private static CorrectiveRetrievalService Service(IRetrievalQualityEvaluator? grader = null)
    {
        var settings = Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } });
        return new(grader ?? new RetrievalQualityEvaluator(settings),
            new AdaptiveRagReranker(new SemanticOnlyRagReranker(), settings, TimeProvider.System),
            settings, TimeProvider.System, NullLogger<CorrectiveRetrievalService>.Instance);
    }
    private static ConnectorExecutionContext Context(Guid id, decimal maximumCost = 1m) => new(id, Guid.NewGuid(),
        RetrievalCandidateLimit: 20, Budget: new(new(TimeSpan.FromSeconds(30), 10, 1000, maximumCost, 20, 10, 10000, 1), DateTimeOffset.UtcNow),
        RagStatus: new());
    private static Microsoft365SearchParameters Parameters(Guid id) => new("policy", ["sharepoint"], new(2026, 1, 1), new(2026, 9, 1),
        new(id, Guid.NewGuid().ToString(), [], []), 10);
    private static Microsoft365SearchRecord Record(double score) => new("sharepoint", "Policy", "Policy text", "p", null, null, "document", null, null, 0.02, score);
    private sealed class FailingGrader : IRetrievalQualityEvaluator
    {
        public Task<RetrievalQualityResult> EvaluateAsync(string query, IReadOnlyCollection<RagPassage> passages, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unavailable");
    }
    private sealed class RecordingReranker : IDedicatedRagReranker
    {
        public int Calls { get; private set; }
        public decimal EstimateCost(IReadOnlyList<RagPassage> passages) => 0.001m;
        public Task<IReadOnlyList<RagPassage>> RerankAsync(string query, IReadOnlyList<RagPassage> passages, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(passages); }
    }
}
