using AssistantCore.Service.Application.Models.Messages.Rag;
using AssistantCore.Service.Application.Services.Messages.Rag;

namespace AssistantCore.Service.Tests.Rag;

/// <summary>
/// Une citation valide ne prouve pas qu'une affirmation est supportee. Le risque
/// est qu'une phrase emprunte ses termes a deux documents differents : chaque
/// terme existe dans les preuves, mais aucun document ne soutient l'affirmation
/// complete.
/// </summary>
public sealed class CrossDocumentGroundednessTests
{
    [Fact]
    public async Task Given_AClaimMixingTwoDocuments_When_EvaluateAsync_Then_ItIsNotConsideredSupported()
    {
        // Given
        // Le justificatif appartient au conge maladie, pas au conge parental.
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var evidence = new RagPassage[]
        {
            new("doc-a", "Le conge parental est de 5 jours.", "doc-a"),
            new("doc-b", "Le conge maladie necessite un justificatif apres 3 jours.", "doc-b")
        };

        // When
        var result = await evaluator.EvaluateAsync(
            "Le conge parental necessite un justificatif.",
            evidence);

        // Then
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task Given_AClaimSupportedByASingleDocument_When_EvaluateAsync_Then_ItRemainsSupported()
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var evidence = new RagPassage[]
        {
            new("doc-a", "Le conge parental est de 5 jours.", "doc-a"),
            new("doc-b", "Le conge maladie necessite un justificatif apres 3 jours.", "doc-b")
        };

        // When
        var result = await evaluator.EvaluateAsync("Le conge parental est de 5 jours.", evidence);

        // Then
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Given_TwoClaimsEachSupportedByItsOwnDocument_When_EvaluateAsync_Then_TheAnswerIsSupported()
    {
        // Given
        // Une reponse peut legitimement synthetiser deux documents, a condition que
        // chaque phrase soit soutenue par l'un d'eux.
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var evidence = new RagPassage[]
        {
            new("doc-a", "Le conge parental est de 5 jours.", "doc-a"),
            new("doc-b", "Le conge maladie necessite un justificatif apres 3 jours.", "doc-b")
        };

        // When
        var result = await evaluator.EvaluateAsync(
            "Le conge parental est de 5 jours. Le conge maladie necessite un justificatif apres 3 jours.",
            evidence);

        // Then
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Given_ATrueClaimFollowedByAnInventedOne_When_EvaluateAsync_Then_TheAnswerIsRejected()
    {
        // Given
        // La source ne prouve que la date de debut; la date de fin est inventee.
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var evidence = new RagPassage[]
        {
            new("contrat", "Le contrat commence le 1er janvier 2026.", "contrat")
        };

        // When
        var result = await evaluator.EvaluateAsync(
            "Le contrat commence le 1er janvier 2026. Il se termine le 31 decembre 2028.",
            evidence);

        // Then
        Assert.False(result.Passed);
    }
}
