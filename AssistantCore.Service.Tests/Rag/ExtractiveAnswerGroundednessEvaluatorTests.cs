using AssistantCore.Service.Application.Models.Messages.Rag;
using AssistantCore.Service.Application.Services.Messages.Rag;

namespace AssistantCore.Service.Tests.Rag;

public sealed class ExtractiveAnswerGroundednessEvaluatorTests
{
    [Theory, AutoDomainData]
    public async Task Given_AFactBasedSynthesis_When_EvaluateAsync_Then_ReturnsSupportedResult(Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var answer = "La pression sur le seuil de rentabilité est le risque principal, avec un chiffre d'affaires de 100000 et un seuil de 98000.";
        var evidence = new[]
        {
            new RagPassage(
                id.ToString(),
                "Atelier Nordik indique un chiffre d'affaires de 100000. Le seuil de rentabilité est de 98000. La pression sur le seuil de rentabilité est élevée.",
                id.ToString())
        };

        // When
        var result = await evaluator.EvaluateAsync(answer, evidence, CancellationToken.None);

        // Then
        Assert.True(result.Passed);
        Assert.Equal(1, result.Confidence);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnsupportedNumber_When_EvaluateAsync_Then_ReturnsUnsupportedResult(Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var answer = "La marge commerciale est insuffisante avec un taux de 12 pour cent.";
        var evidence = new[]
        {
            new RagPassage(
                id.ToString(),
                "La marge commerciale disponible est de 18 pour cent.",
                id.ToString())
        };

        // When
        var result = await evaluator.EvaluateAsync(answer, evidence, CancellationToken.None);

        // Then
        Assert.False(result.Passed);
        Assert.Equal(0, result.Confidence);
    }
    [Theory]
    [InlineAutoDomainData("La marge commerciale est de 8 %.", "La marge commerciale est de 9 %.", false)]
    [InlineAutoDomainData("La marge commerciale est de 12,5 %.", "La marge commerciale est de 12.5 %.", true)]
    [InlineAutoDomainData("Le chiffre d'affaires est de 100 000.", "Le chiffre d'affaires est de 100000.", true)]
    [InlineAutoDomainData("Le chiffre d'affaires est de 100\u202f000.", "Le chiffre d'affaires est de 100000.", true)]
    [InlineAutoDomainData("La créance douteuse est de 45250.", "La créance douteuse est de 45,250.", true)]
    [InlineAutoDomainData("La créance douteuse estimée est de 45250.", "La balance indique 90500 et un risque de 50 %.", true)]
    [InlineAutoDomainData("La créance douteuse estimée est de 90500 × 50 % = 45250.", "La balance indique 90500 et un risque de 50 %.", true)]
    [InlineAutoDomainData("La créance douteuse estimée représente 45250 / 90500 = 50 %.", "La balance indique 90500 et un risque de 50 %.", true)]
    [InlineAutoDomainData("La créance douteuse estimée est de 90500 × 0,5 = 45250.", "La balance indique 90500 et un risque de 50 %.", true)]
    [InlineAutoDomainData("La créance douteuse estimée est de 90500 × 50 % = 45250.", "La balance indique 90500 et un coefficient de risque de 0,5.", true)]
    [InlineAutoDomainData("La perte économique potentielle est de 45 000 × (1 - 60 %) = 18 000.", "La créance MécanoPlus s'élève à 45 000 $ et la récupération probable est estimée à 60 %.", true)]
    [InlineAutoDomainData("La marge commerciale est de 35000 / 175000 = 20 %.", "MetalPro indique 35000 de marge commerciale pour 175000 de chiffre d'affaires.", true)]
    [InlineAutoDomainData("La marge commerciale est de 20 %.", "MetalPro indique 35000 de marge commerciale pour 175000 de chiffre d'affaires.", true)]
    [InlineAutoDomainData("La créance douteuse estimée est de 45250. L'écart est de 50000 - 45250 = 4750.", "La balance indique 90500 et un risque de 50 %. Le plafond est de 50000.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité laisse 100 000 − 98 000 = 2 000.", "Le seuil de rentabilité est de 98000 pour un chiffre d'affaires de 100000.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité laisse 100000 - 98000 = 3000.", "Le seuil de rentabilité est de 98000 pour un chiffre d'affaires de 100000. La facture est de 3000.", false)]
    [InlineAutoDomainData("Le seuil de rentabilité laisse 100000 - 97000 = 3000.", "Le seuil de rentabilité est de 98000 pour un chiffre d'affaires de 100000.", false)]
    [InlineAutoDomainData("La marge commerciale est de 10 / 0 = 0.", "La marge commerciale mentionne 10 et 0.", false)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 115445.71.", "Le seuil de rentabilité est de 115,445.71.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 115.445,71.", "Le seuil de rentabilité est de 115 445,71.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 115,445.71.", "Le seuil de rentabilité est de 115445.71.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 1,115,445.71.", "Le seuil de rentabilité est de 1115445.71.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 1.115.445,71.", "Le seuil de rentabilité est de 1115445.71.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 1,115,445.", "Le seuil de rentabilité est de 1115445.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 445.71.", "Le seuil de rentabilité est de 115,445.71.", false)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 115445.72.", "Le seuil de rentabilité est de 115,445.71.", false)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 808120 / 7 = 115445.71.", "Le seuil de rentabilité utilise 808120 et 7.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 115445.71.", "Le seuil de rentabilité utilise 808120 et 7.", true)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 808120 / 7 = 115445.72.", "Le seuil de rentabilité utilise 808120 et 7.", false)]
    [InlineAutoDomainData("Le seuil de rentabilité est de 115445.72.", "Le seuil de rentabilité utilise 808120 et 7.", false)]
    [InlineAutoDomainData("La marge commerciale est de 100 / 300 = 33.33 %.", "La marge commerciale utilise 100 et 300.", true)]
    [InlineAutoDomainData("La marge commerciale est de 100 / 300 = 33.34 %.", "La marge commerciale utilise 100 et 300.", false)]
    [InlineAutoDomainData("La marge commerciale est de 100 / 300 = 33 %.", "La marge commerciale utilise 100 et 300.", false)]
    [InlineAutoDomainData("La marge commerciale est de 100 / 300 = 33.3 %.", "La marge commerciale utilise 100 et 300.", true)]
    [InlineAutoDomainData("La créance douteuse est de 1.25 × 50 % = 0.63.", "La créance douteuse utilise 1.25 et 50 %.", true)]
    [InlineAutoDomainData("La créance douteuse est de 1.25 × 50 % = 0.64.", "La créance douteuse utilise 1.25 et 50 %.", false)]
    public async Task Given_NumericEvidence_When_EvaluateAsync_Then_ValidatesNumbersAndExplicitCalculations(
        string answer, string content, bool expected, Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var evidence = new[] { new RagPassage(id.ToString(), content, id.ToString()) };

        // When
        var result = await evaluator.EvaluateAsync(answer, evidence, CancellationToken.None);

        // Then
        Assert.Equal(expected, result.Passed);
    }

    [Theory, AutoDomainData]
    public async Task Given_OneUnsupportedNumberAmongSupportedClaims_When_EvaluateAsync_Then_RejectsAnswer(Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        const string content = "La marge commerciale est de 18 pour cent.";
        var answer = string.Join(" ", Enumerable.Repeat(content, 4)) + " La marge commerciale est de 12 pour cent.";
        var evidence = new[] { new RagPassage(id.ToString(), content, id.ToString()) };

        // When
        var result = await evaluator.EvaluateAsync(answer, evidence, CancellationToken.None);

        // Then
        Assert.False(result.Passed);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnsupportedNumber_When_EvaluateAsync_Then_IdentifiesRejectedNumber(Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var evidence = new[] { new RagPassage(id.ToString(), "La marge commerciale est de 18 %.", id.ToString()) };

        // When
        var result = await evaluator.EvaluateAsync("La marge commerciale est de 12 %.", evidence, CancellationToken.None);

        // Then
        Assert.False(result.Passed);
        Assert.Contains("Number 12 at position", result.Reason);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnIncorrectCalculation_When_EvaluateAsync_Then_IdentifiesCalculationFailure(Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var evidence = new[] { new RagPassage(id.ToString(), "Le seuil de rentabilité est de 98000 pour un chiffre d'affaires de 100000.", id.ToString()) };

        // When
        var result = await evaluator.EvaluateAsync("Le seuil de rentabilité laisse 100000 - 98000 = 3000.", evidence, CancellationToken.None);

        // Then
        Assert.False(result.Passed);
        Assert.Contains("Calculation result is incorrect", result.Reason);
    }

    [Theory, AutoDomainData]
    public async Task Given_NumberedAnswerWithSupportedFinancialNumbers_When_EvaluateAsync_Then_IgnoresListMarkers(Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var answer = "1. La marge commerciale est de 18 %.\n2. Le seuil de rentabilité est de 98000 pour un chiffre d'affaires de 100000.";
        var evidence = new[]
        {
            new RagPassage(
                id.ToString(),
                "La marge commerciale est de 18 %. Le seuil de rentabilité est de 98000 pour un chiffre d'affaires de 100000.",
                id.ToString())
        };

        // When
        var result = await evaluator.EvaluateAsync(answer, evidence, CancellationToken.None);

        // Then
        Assert.True(result.Passed);
    }

    [Theory, AutoDomainData]
    public async Task Given_CitedAnswerWithSupportedFinancialNumbers_When_EvaluateAsync_Then_IgnoresCitationMarkers(Guid id)
    {
        // Given
        var evaluator = new ExtractiveAnswerGroundednessEvaluator();
        var answer = "La marge commerciale est de 18 % [1].";
        var evidence = new[]
        {
            new RagPassage(id.ToString(), "La marge commerciale est de 18 %.", id.ToString())
        };

        // When
        var result = await evaluator.EvaluateAsync(answer, evidence, CancellationToken.None);

        // Then
        Assert.True(result.Passed);
    }

}
