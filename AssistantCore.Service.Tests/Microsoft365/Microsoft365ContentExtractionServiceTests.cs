using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ContentExtractionServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_ARegisteredExtractor_When_ExtractAsync_Then_RoutesTheRequest(
        string fileName)
    {
        // Given
        fileName += ".docx";
        var expected = new Microsoft365ContentExtractionResult(
            Microsoft365ContentExtractionStatus.Success,
            [],
            []);
        var extractor = new RecordingExtractor(expected, canExtract: true);
        var service = new Microsoft365ContentExtractionService([extractor]);
        var request = new Microsoft365ContentExtractionRequest(
            fileName,
            null,
            new MemoryStream());

        // When
        var result = await service.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Same(expected, result);
        Assert.Same(request, extractor.Request);
    }

    [Theory, AutoDomainData]
    public async Task Given_NoRegisteredExtractor_When_ExtractAsync_Then_ReturnsUnsupportedFormat(
        string fileName)
    {
        // Given
        var service = new Microsoft365ContentExtractionService([]);
        var request = new Microsoft365ContentExtractionRequest(
            fileName,
            null,
            new MemoryStream());

        // When
        var result = await service.ExtractAsync(request, CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365ContentExtractionStatus.UnsupportedFormat, result.Status);
        Assert.Empty(result.Units);
    }

    [Theory, AutoDomainData]
    public async Task Given_MultipleMatchingExtractors_When_ExtractAsync_Then_ThrowsInvalidOperationException(
        string fileName)
    {
        // Given
        var result = Microsoft365ContentExtractionResult.Unsupported();
        var service = new Microsoft365ContentExtractionService(
            [new RecordingExtractor(result, true), new RecordingExtractor(result, true)]);
        var request = new Microsoft365ContentExtractionRequest(
            fileName,
            null,
            new MemoryStream());

        // When
        var exception = await Record.ExceptionAsync(() =>
            service.ExtractAsync(request, CancellationToken.None));

        // Then
        Assert.IsType<InvalidOperationException>(exception);
    }

    private sealed class RecordingExtractor(
        Microsoft365ContentExtractionResult result,
        bool canExtract) : IMicrosoft365ContentExtractor
    {
        public Microsoft365ContentExtractionRequest? Request { get; private set; }

        public bool CanExtract(string fileName, string? mimeType) => canExtract;

        public Task<Microsoft365ContentExtractionResult> ExtractAsync(
            Microsoft365ContentExtractionRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
