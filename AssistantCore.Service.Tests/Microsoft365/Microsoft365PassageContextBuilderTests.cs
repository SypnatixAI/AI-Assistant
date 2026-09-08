using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365PassageContextBuilderTests
{
    [Fact]
    public void Given_TwoSectionsSharingASentence_When_BuildEmbeddingContent_Then_TheyBecomeDistinguishable()
    {
        // Given
        // La meme phrase apparait dans deux sections et n'a pas le meme sens :
        // sans contexte, le moteur ne peut pas les distinguer.
        const string sharedSentence = "Maximum remboursable : 75 $ par jour.";
        var international = CreatePassage(
            sharedSentence,
            "Politique de remboursement des deplacements",
            "Repas lors de deplacements internationaux");
        var domestic = CreatePassage(
            sharedSentence,
            "Politique de remboursement des deplacements",
            "Repas lors de deplacements nationaux");

        // When
        var internationalContent = Microsoft365PassageContextBuilder.BuildEmbeddingContent(international);
        var domesticContent = Microsoft365PassageContextBuilder.BuildEmbeddingContent(domestic);

        // Then
        Assert.NotEqual(internationalContent, domesticContent);
        Assert.Contains("[Section] Repas lors de deplacements internationaux", internationalContent);
        Assert.Contains("[Section] Repas lors de deplacements nationaux", domesticContent);
        Assert.Contains(sharedSentence, internationalContent);
        Assert.Contains(sharedSentence, domesticContent);
    }

    [Fact]
    public void Given_APassage_When_BuildEmbeddingContent_Then_TheOriginalContentIsNeverAltered()
    {
        // Given
        var passage = CreatePassage("Le texte du document.", "Titre", "Section");

        // When
        Microsoft365PassageContextBuilder.BuildEmbeddingContent(passage);

        // Then
        // Le contenu cite reste intact : la contextualisation ne vit que dans le
        // texte soumis a l'embedding.
        Assert.Equal("Le texte du document.", passage.Content);
    }

    [Fact]
    public void Given_NoTitleAndNoSection_When_BuildEmbeddingContent_Then_TheContentIsReturnedAsIs()
    {
        // Given
        // Un format sans structure explicite doit continuer de fonctionner.
        var passage = new Microsoft365SearchPassage("chunk", string.Empty, "Texte brut.");

        // When
        var result = Microsoft365PassageContextBuilder.BuildEmbeddingContent(passage);

        // Then
        Assert.Equal("Texte brut.", result);
    }

    [Fact]
    public void Given_AContentAlreadyStartingWithItsSection_When_BuildEmbeddingContent_Then_TheSectionIsNotRepeated()
    {
        // Given
        var passage = CreatePassage("Acces au batiment\nLa procedure exige un badge.", "Titre", "Acces au batiment");

        // When
        var result = Microsoft365PassageContextBuilder.BuildEmbeddingContent(passage);

        // Then
        Assert.DoesNotContain("[Section]", result);
        Assert.Contains("[Document] Titre", result);
    }

    [Fact]
    public void Given_TheSamePassageTwice_When_BuildEmbeddingContent_Then_TheResultIsDeterministic()
    {
        // Given
        var passage = CreatePassage("Le texte.", "Titre", "Section");

        // When
        var first = Microsoft365PassageContextBuilder.BuildEmbeddingContent(passage);
        var second = Microsoft365PassageContextBuilder.BuildEmbeddingContent(passage);

        // Then
        Assert.Equal(first, second);
    }

    [Fact]
    public void Given_ASectionOnly_When_BuildEmbeddingContent_Then_OnlyTheAvailableContextIsAdded()
    {
        // Given
        // Aucun contexte n'est invente : seules les metadonnees presentes sont utilisees.
        var passage = new Microsoft365SearchPassage(
            "chunk",
            string.Empty,
            "Le texte.",
            SectionTitle: "Section");

        // When
        var result = Microsoft365PassageContextBuilder.BuildEmbeddingContent(passage);

        // Then
        Assert.DoesNotContain("[Document]", result);
        Assert.Contains("[Section] Section", result);
        Assert.Contains("Le texte.", result);
    }

    private static Microsoft365SearchPassage CreatePassage(
        string content,
        string title,
        string sectionTitle) =>
        new("chunk", title, content, SectionTitle: sectionTitle);
}
