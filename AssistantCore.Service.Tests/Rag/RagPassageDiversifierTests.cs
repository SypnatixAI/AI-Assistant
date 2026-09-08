using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Rag;
using AssistantCore.Service.Application.Services.Messages.Rag;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Rag;

public sealed class RagPassageDiversifierTests
{
    [Fact]
    public void Given_AQuestionNeedingTwoDocuments_When_Diversify_Then_TheSecondSourceEntersTheContext()
    {
        // Given
        // Le classement place quatre extraits de la politique RH devant la
        // procedure paie, pourtant necessaire pour repondre a la seconde moitie
        // de la question.
        var diversifier = CreateDiversifier();
        var ranked = new RagPassage[]
        {
            new("rh-12", "Les vacances sont acquises mensuellement.", "politique-rh", 3.0),
            new("rh-13", "Le solde de vacances se reporte une fois.", "politique-rh", 2.95),
            new("rh-14", "Une demande de vacances passe par le manager.", "politique-rh", 2.9),
            new("rh-15", "Les vacances non prises sont perdues en mars.", "politique-rh", 2.85),
            new("paie-4", "La paie ajuste le solde de vacances chaque mois.", "procedure-paie", 2.8)
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        Assert.Equal(ranked.Length, result.Count);
        Assert.Equal("paie-4", result[2].Reference);
        Assert.Equal(
            ["politique-rh", "politique-rh", "procedure-paie", "politique-rh", "politique-rh"],
            result.Select(passage => passage.SourceIdentity));
    }

    [Fact]
    public void Given_AMuchWeakerAlternative_When_Diversify_Then_TheRankingIsNotBypassed()
    {
        // Given
        // La seule autre source est tres loin derriere : la promouvoir
        // reviendrait a contourner le classement.
        var diversifier = CreateDiversifier();
        var ranked = new RagPassage[]
        {
            new("rh-12", "Premier extrait.", "politique-rh", 3.0),
            new("rh-13", "Deuxieme extrait.", "politique-rh", 2.9),
            new("rh-14", "Troisieme extrait.", "politique-rh", 2.8),
            new("faq-1", "Extrait peu pertinent.", "faq-rh", 0.4)
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        Assert.Equal(
            ["rh-12", "rh-13", "rh-14", "faq-1"],
            result.Select(passage => passage.Reference));
    }

    [Fact]
    public void Given_NearIdenticalPassagesFromTheSameSource_When_Diversify_Then_TheRedundantOneIsRemoved()
    {
        // Given
        var diversifier = CreateDiversifier();
        var ranked = new RagPassage[]
        {
            new("rh-12", "Les vacances sont acquises mensuellement.", "politique-rh", 3.0),
            new("rh-12-bis", "Les vacances sont acquises mensuellement !", "politique-rh", 2.9),
            new("paie-4", "La paie ajuste le solde chaque mois.", "procedure-paie", 2.8)
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        Assert.Equal(["rh-12", "paie-4"], result.Select(passage => passage.Reference));
    }

    [Fact]
    public void Given_IdenticalContentFromTwoSources_When_Diversify_Then_BothRemainDistinctEvidence()
    {
        // Given
        // Deux documents differents peuvent legitimement se citer l'un l'autre :
        // ce sont deux preuves, pas un doublon.
        var diversifier = CreateDiversifier();
        var ranked = new RagPassage[]
        {
            new("rh-12", "La limite est de cinq jours.", "politique-rh", 3.0),
            new("paie-4", "La limite est de cinq jours.", "procedure-paie", 2.9)
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        Assert.Equal(["rh-12", "paie-4"], result.Select(passage => passage.Reference));
    }

    [Fact]
    public void Given_ASingleSource_When_Diversify_Then_SeveralPassagesRemainAvailable()
    {
        // Given
        // Une question mono-document doit continuer de recevoir plusieurs extraits.
        var diversifier = CreateDiversifier();
        var ranked = new RagPassage[]
        {
            new("rh-12", "Premier extrait distinct.", "politique-rh", 3.0),
            new("rh-13", "Deuxieme extrait different.", "politique-rh", 2.9),
            new("rh-14", "Troisieme contenu sans rapport.", "politique-rh", 2.8)
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        Assert.Equal(3, result.Count);
        Assert.Equal(
            ["rh-12", "rh-13", "rh-14"],
            result.Select(passage => passage.Reference));
    }

    [Fact]
    public void Given_DiversificationDisabled_When_Diversify_Then_TheRankingIsReturnedUnchanged()
    {
        // Given
        var diversifier = new RagPassageDiversifier(Options.Create(new RagOptions
        {
            Diversification = new RagDiversificationOptions { Enabled = false }
        }));
        var ranked = new RagPassage[]
        {
            new("rh-12", "Texte.", "politique-rh", 3.0),
            new("rh-13", "Texte.", "politique-rh", 2.9)
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        Assert.Same(ranked, result);
    }

    [Fact]
    public void Given_PassagesWithoutScores_When_Diversify_Then_TheOriginalOrderPrevails()
    {
        // Given
        // Sans score, aucune comparaison n'est possible : le classement d'origine
        // fait autorite plutot qu'une promotion arbitraire.
        var diversifier = CreateDiversifier();
        var ranked = new RagPassage[]
        {
            new("rh-12", "Premier extrait.", "politique-rh"),
            new("rh-13", "Deuxieme extrait.", "politique-rh"),
            new("rh-14", "Troisieme extrait.", "politique-rh"),
            new("paie-4", "Extrait de paie.", "procedure-paie")
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        Assert.Equal(
            ["rh-12", "rh-13", "rh-14", "paie-4"],
            result.Select(passage => passage.Reference));
    }

    [Fact]
    public void Given_AnyRanking_When_Diversify_Then_NoEvidenceIsInventedOrDuplicated()
    {
        // Given
        var diversifier = CreateDiversifier();
        var ranked = new RagPassage[]
        {
            new("a-1", "Contenu alpha.", "alpha", 3.0),
            new("a-2", "Autre contenu alpha.", "alpha", 2.9),
            new("b-1", "Contenu beta.", "beta", 2.85),
            new("a-3", "Troisieme contenu alpha.", "alpha", 2.8)
        };

        // When
        var result = diversifier.Diversify(ranked);

        // Then
        var references = result.Select(passage => passage.Reference).ToArray();
        Assert.Equal(references.Length, references.Distinct(StringComparer.Ordinal).Count());
        Assert.All(result, passage => Assert.Contains(passage, ranked));
    }

    private static RagPassageDiversifier CreateDiversifier() =>
        new(Options.Create(new RagOptions
        {
            Diversification = new RagDiversificationOptions
            {
                Enabled = true,
                MaximumConsecutiveFromSameSource = 2,
                MaximumPromotionScoreGap = 0.2,
                NearDuplicateOverlapThreshold = 0.9
            }
        }));
}
