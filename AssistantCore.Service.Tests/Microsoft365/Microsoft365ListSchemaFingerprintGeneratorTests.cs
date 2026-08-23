using System.Text.Json;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ListSchemaFingerprintGeneratorTests
{
    [Theory, AutoDomainData]
    public void Given_EquivalentSchemasInDifferentOrders_When_CreateFingerprint_Then_ReturnsSameFingerprint(
        string firstColumnId,
        string secondColumnId)
    {
        // Given
        var firstSchema = new[]
        {
            CreateColumn(secondColumnId, $"{{\"name\":\"Status\",\"choice\":{{\"choices\":[\"Open\",\"Closed\"]}},\"id\":\"{secondColumnId}\"}}"),
            CreateColumn(firstColumnId, $"{{\"text\":{{}},\"id\":\"{firstColumnId}\",\"name\":\"Title\"}}")
        };
        var equivalentSchema = new[]
        {
            CreateColumn(firstColumnId, $"{{\"name\":\"Title\",\"id\":\"{firstColumnId}\",\"text\":{{}}}}"),
            CreateColumn(secondColumnId, $"{{\"id\":\"{secondColumnId}\",\"choice\":{{\"choices\":[\"Open\",\"Closed\"]}},\"name\":\"Status\"}}")
        };
        var generator = new Microsoft365ListSchemaFingerprintGenerator();

        // When
        var firstFingerprint = generator.CreateFingerprint(firstSchema);
        var secondFingerprint = generator.CreateFingerprint(equivalentSchema);

        // Then
        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.Equal(64, firstFingerprint.Length);
    }

    [Theory, AutoDomainData]
    public void Given_AColumnDefinitionChange_When_CreateFingerprint_Then_ReturnsDifferentFingerprint(
        string columnId)
    {
        // Given
        var original = new[] { CreateColumn(columnId, $"{{\"id\":\"{columnId}\",\"text\":{{}}}}") };
        var changed = new[] { CreateColumn(columnId, $"{{\"id\":\"{columnId}\",\"number\":{{}}}}") };
        var generator = new Microsoft365ListSchemaFingerprintGenerator();

        // When
        var originalFingerprint = generator.CreateFingerprint(original);
        var changedFingerprint = generator.CreateFingerprint(changed);

        // Then
        Assert.NotEqual(originalFingerprint, changedFingerprint);
    }

    private static Microsoft365ListColumn CreateColumn(string id, string json)
    {
        using var document = JsonDocument.Parse(json);
        return new Microsoft365ListColumn(id, document.RootElement.Clone());
    }
}
