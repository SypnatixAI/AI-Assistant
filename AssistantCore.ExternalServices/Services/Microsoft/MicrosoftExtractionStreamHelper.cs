namespace AssistantCore.ExternalServices.Services.Microsoft;

internal static class MicrosoftExtractionStreamHelper
{
    public static async Task<MemoryStream?> CopyWithinLimitAsync(
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
}
