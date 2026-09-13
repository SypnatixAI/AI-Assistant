using AssistantCore.ExternalServices.Entities.Microsoft;
using UglyToad.PdfPig;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftPdfContentExtractorClient
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public async Task<MicrosoftPdfExtractionResult> ExtractAsync(
        Stream content,
        long? contentLength,
        long maximumFileSizeBytes,
        int maximumPages,
        int maximumExtractedCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (maximumFileSizeBytes <= 0 || maximumPages <= 0 || maximumExtractedCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFileSizeBytes));
        }
        if (contentLength is < 0 || contentLength > maximumFileSizeBytes)
        {
            return Result(MicrosoftPdfExtractionStatus.TooLarge);
        }

        await using var copy = await CopyWithinLimitAsync(
            content,
            maximumFileSizeBytes,
            cancellationToken);
        if (copy is null || !HasPdfSignature(copy))
        {
            return Result(MicrosoftPdfExtractionStatus.CorruptedDocument);
        }

        try
        {
            using var document = PdfDocument.Open(copy);
            if (document.NumberOfPages > maximumPages)
            {
                return Result(MicrosoftPdfExtractionStatus.TooLarge);
            }

            var pages = new List<MicrosoftPdfPage>();
            var characterCount = 0;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = Normalize(page.Text);
                if (text.Length == 0)
                {
                    continue;
                }

                characterCount = checked(characterCount + text.Length);
                if (characterCount > maximumExtractedCharacters)
                {
                    return Result(MicrosoftPdfExtractionStatus.TooLarge);
                }

                pages.Add(new MicrosoftPdfPage(page.Number, text));
            }

            return pages.Count == 0
                ? Result(MicrosoftPdfExtractionStatus.EmptyDocument)
                : new MicrosoftPdfExtractionResult(MicrosoftPdfExtractionStatus.Success, pages);
        }
        catch (ArgumentException)
        {
            return Result(MicrosoftPdfExtractionStatus.EncryptedDocument);
        }
        catch (InvalidOperationException)
        {
            return Result(MicrosoftPdfExtractionStatus.CorruptedDocument);
        }
    }

    private static async Task<MemoryStream?> CopyWithinLimitAsync(
        Stream source,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var destination = new MemoryStream();
        var buffer = new byte[81920];
        long totalBytes = 0;
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                destination.Position = 0;
                return destination;
            }

            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
            {
                await destination.DisposeAsync();
                return null;
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static bool HasPdfSignature(Stream content)
    {
        Span<byte> signature = stackalloc byte[PdfSignature.Length];
        return content.Read(signature) == PdfSignature.Length
            && signature.SequenceEqual(PdfSignature);
    }

    private static MicrosoftPdfExtractionResult Result(MicrosoftPdfExtractionStatus status) =>
        new(status, []);

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
