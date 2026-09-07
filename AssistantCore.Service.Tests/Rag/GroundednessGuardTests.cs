using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;
using AssistantCore.Service.Application.Models.Messages.Orchestration;
using AssistantCore.Service.Application.Models.Messages.Rag;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Rag;

public sealed class GroundednessGuardTests
{
    [Theory]
    [InlineAutoDomainData(true, false, true)]
    [InlineAutoDomainData(false, false, false)]
    [InlineAutoDomainData(false, true, false)]
    public async Task Given_ValidationOutcome_When_ValidateAsync_Then_PreservesOnlySupportedAnswer(bool supported, bool fails, bool preserve, Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new RetrievedEvidence("e", "internal", "Policy", "La limite est 5000.", "policy", null, null);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [evidence])]);
        var original = new MessageOrchestrationResult("La limite est 5000.", "model", [evidence], [], state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(new Evaluator(supported, fails),
            Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }), TimeProvider.System,
            NullLogger<AnswerGroundednessGuard>.Instance);
        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);
        // Then
        if (preserve) Assert.Same(original, result);
        else
        {
            Assert.Equal(AnswerGroundednessGuard.InsufficientEvidenceAnswer, result.Answer);
            Assert.Empty(result.CitedEvidence);
            Assert.Contains("rag.groundedness.unverified", result.Warnings);
            Assert.Contains(fails ? "rag.groundedness.validator_error" : "rag.groundedness.content_rejected", result.Warnings);
        }
    }

    [Theory, AutoDomainData]
    public async Task Given_DisabledChecks_When_ValidateAsync_Then_RetainsCurrentBehavior(Guid id)
    {
        // Given
        var state = CreateState(id);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [])]);
        var original = new MessageOrchestrationResult("Answer", "model", [], [], state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(new Evaluator(false, true), Options.Create(new RagOptions()),
            TimeProvider.System, NullLogger<AnswerGroundednessGuard>.Instance);
        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);
        // Then
        Assert.Same(original, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_CancelledValidator_When_ValidateAsync_Then_PropagatesCancellation(Guid id)
    {
        // Given
        var state = CreateState(id);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [])]);
        var original = new MessageOrchestrationResult("Answer", "model", [], [], state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(new ExtractiveAnswerGroundednessEvaluator(),
            Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }),
            TimeProvider.System, NullLogger<AnswerGroundednessGuard>.Instance);
        using var source = new CancellationTokenSource();
        source.Cancel();
        // When
        var action = () => guard.ValidateAsync(state, original, source.Token);
        // Then
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }

    [Theory]
    [InlineAutoDomainData("100000 - 98000 = 2000", true)]
    [InlineAutoDomainData("100000 - 98000 = 3000", false)]
    public async Task Given_FinancialCalculation_When_ValidateAsync_Then_PreservesOnlyVerifiedResult(
        string calculation, bool preserve, Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new RetrievedEvidence("e", "internal", "Comptabilité",
            "Le seuil de rentabilité est de 98000 pour un chiffre d'affaires de 100000.", "accounting", null, null);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [evidence])]);
        var original = new MessageOrchestrationResult($"Le seuil de rentabilité laisse {calculation}.",
            "model", [evidence], [], state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(new ExtractiveAnswerGroundednessEvaluator(),
            Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }), TimeProvider.System,
            NullLogger<AnswerGroundednessGuard>.Instance);

        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);

        // Then
        if (preserve) Assert.Same(original, result);
        else Assert.Contains("rag.groundedness.content_rejected", result.Warnings);
    }

    [Theory, AutoDomainData]
    public async Task Given_FinancialRiskComparison_When_ValidateAsync_Then_PreservesGroundedSynthesis(Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new[]
        {
            new RetrievedEvidence("e1", "internal", "Performance commerciale MetalPro",
                "MetalPro affiche une marge commerciale de 18 % alors que l'objectif interne est de 25 %.", "metalpro", null, null),
            new RetrievedEvidence("e2", "internal", "Dossier comptable Atelier Nordik",
                "Atelier Nordik conserve une créance client de 12000 $ échue depuis 90 jours.", "nordik", null, null),
            new RetrievedEvidence("e3", "internal", "Calculs financiers",
                "Le chiffre d'affaires prévisionnel est de 100000 $. Le seuil de rentabilité est de 98000 $. La marge de sécurité est donc de 2000 $, soit 2 % du chiffre d'affaires.", "calculs", null, null)
        };
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", evidence)]);
        var original = new MessageOrchestrationResult(
            "La situation qui présente le plus grand risque financier à court terme est la pression sur le seuil de rentabilité. La marge de sécurité n'est que de 2000 $, soit 2 % du chiffre d'affaires, car le chiffre d'affaires prévisionnel de 100000 $ dépasse à peine le seuil de rentabilité de 98000 $. La marge commerciale de MetalPro est insuffisante à 18 % contre un objectif de 25 %, et la créance douteuse d'Atelier Nordik atteint 12000 $ échus depuis 90 jours, mais le risque immédiat le plus serré vient du seuil de rentabilité.",
            "model", evidence, [], state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(new ExtractiveAnswerGroundednessEvaluator(),
            Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }), TimeProvider.System,
            NullLogger<AnswerGroundednessGuard>.Instance);

        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);

        // Then
        Assert.Same(original, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_GroundedFinancialSynthesisWithOneUnmatchedNumber_When_ValidateAsync_Then_PreservesAnswer(Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new[]
        {
            new RetrievedEvidence("e1", "internal", "Performance commerciale MetalPro",
                "MetalPro affiche une marge commerciale de 18 % alors que l'objectif interne est de 25 %.", "metalpro", null, null),
            new RetrievedEvidence("e2", "internal", "Dossier comptable Atelier Nordik",
                "Atelier Nordik conserve une créance client de 12000 $ échue depuis 90 jours.", "nordik", null, null),
            new RetrievedEvidence("e3", "internal", "Calculs financiers",
                "Le chiffre d'affaires prévisionnel est de 100000 $. Le seuil de rentabilité est de 98000 $. La marge de sécurité est donc de 2000 $, soit 2 % du chiffre d'affaires.", "calculs", null, null)
        };
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", evidence)]);
        var original = new MessageOrchestrationResult(
            "La pression sur le seuil de rentabilité est le risque financier le plus immédiat, même si le montant 448007 doit être ignoré faute d'appui direct. La marge de sécurité est de 2000 $, soit 2 % du chiffre d'affaires, avec un chiffre d'affaires de 100000 $ et un seuil de rentabilité de 98000 $. MetalPro reste sous l'objectif avec 18 % contre 25 %, et Atelier Nordik porte une créance de 12000 $ échue depuis 90 jours.",
            "model", evidence, [], state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(new ExtractiveAnswerGroundednessEvaluator(),
            Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }), TimeProvider.System,
            NullLogger<AnswerGroundednessGuard>.Instance);

        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);

        // Then
        Assert.Same(original, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_UnsupportedStandaloneFinancialNumber_When_ValidateAsync_Then_RejectsAnswer(Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new RetrievedEvidence("e", "internal", "Comptabilité",
            "La limite approuvée est de 5000 $.", "accounting", null, null);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [evidence])]);
        var original = new MessageOrchestrationResult("La limite approuvée est de 6000 $.",
            "model", [evidence], [], state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(new ExtractiveAnswerGroundednessEvaluator(),
            Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }), TimeProvider.System,
            NullLogger<AnswerGroundednessGuard>.Instance);

        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);

        // Then
        Assert.Equal(AnswerGroundednessGuard.InsufficientEvidenceAnswer, result.Answer);
        Assert.Contains("rag.groundedness.content_rejected", result.Warnings);
    }

    [Theory, AutoDomainData]
    public async Task Given_ProjectEntityInTitleAndDateInContent_When_ValidateAsync_Then_PreservesGroundedAnswer(Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new RetrievedEvidence(
            "e",
            "SharePoint",
            "Projet Atlas",
            "La date de fin est le 31 décembre 2026.",
            "sharepoint-document",
            null,
            null);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [evidence])]);
        var original = new MessageOrchestrationResult(
            "La date de fin du projet Atlas est le 31 décembre 2026.",
            "model",
            [evidence],
            [],
            state.Budget.Usage);
        var guard = new AnswerGroundednessGuard(
            new ExtractiveAnswerGroundednessEvaluator(),
            Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }),
            TimeProvider.System,
            NullLogger<AnswerGroundednessGuard>.Instance);

        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);

        // Then
        Assert.Same(original, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_PartialProjectDateInEvidence_When_ValidateAsync_Then_PreservesTheDateWithoutAddingAYear(Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new RetrievedEvidence(
            "e",
            "SharePoint",
            "Projet Atlas",
            "Le code du projet Atlas est ORANGE-7429. La date de fin de projet est pour le 1er septembre.",
            "sharepoint-document",
            null,
            null);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [evidence])]);
        var original = new MessageOrchestrationResult(
            "La date de fin du projet Atlas est le 1er septembre.",
            "model",
            [evidence],
            [],
            state.Budget.Usage);
        var guard = CreateGuard();

        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);

        // Then
        Assert.Same(original, result);
    }

    [Theory, AutoDomainData]
    public async Task Given_ProjectDateWithoutYearInEvidence_When_ValidateAsync_Then_RejectsAnInferredYear(Guid id)
    {
        // Given
        var state = CreateState(id);
        var evidence = new RetrievedEvidence(
            "e",
            "SharePoint",
            "Projet Atlas",
            "Le code du projet Atlas est ORANGE-7429. La date de fin de projet est pour le 1er septembre.",
            "sharepoint-document",
            null,
            null);
        state.RecordToolResults([ToolExecutionResult.Succeeded("call", [evidence])]);
        var original = new MessageOrchestrationResult(
            "La date de fin du projet Atlas est le 1er septembre 2026.",
            "model",
            [evidence],
            [],
            state.Budget.Usage);
        var guard = CreateGuard();

        // When
        var result = await guard.ValidateAsync(state, original, CancellationToken.None);

        // Then
        Assert.Equal(AnswerGroundednessGuard.InsufficientEvidenceAnswer, result.Answer);
        Assert.Contains(IAnswerGroundednessGuard.ContentRejectedWarning, result.Warnings);
    }

    private static AnswerGroundednessGuard CreateGuard() => new(
        new ExtractiveAnswerGroundednessEvaluator(),
        Options.Create(new RagOptions { CorrectiveRag = new() { Enabled = true } }),
        TimeProvider.System,
        NullLogger<AnswerGroundednessGuard>.Instance);

    private static MessageOrchestrationState CreateState(Guid id) => MessageOrchestrationState.Start(
        new StartedMessageProcessing(id, id, id, id, "Question"), new SelectedAiModel("provider", "model"), [], [],
        new OrchestrationExecutionLimits(TimeSpan.FromSeconds(30), 10, 10000, 1m, 10, 10, 10000, 1), DateTimeOffset.UtcNow);

    private sealed class Evaluator(bool supported, bool fails) : IAnswerGroundednessEvaluator
    {
        public Task<GroundednessResult> EvaluateAsync(string answer, IReadOnlyCollection<RagPassage> evidence, CancellationToken cancellationToken = default)
        {
            if (fails) throw new InvalidOperationException("Unavailable");
            return Task.FromResult(new GroundednessResult(supported, supported ? 1 : 0, null));
        }
    }
}
