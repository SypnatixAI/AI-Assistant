using System.Net.Http.Headers;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphDriveContentClient(HttpClient httpClient)
{
    public async Task<byte[]> DownloadAsync(
        string graphBaseUrl,
        string accessToken,
        string driveId,
        string driveItemId,
        long maximumBytes,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{graphBaseUrl.TrimEnd('/')}/v1.0/drives/{Uri.EscapeDataString(driveId)}/items/{Uri.EscapeDataString(driveItemId)}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new MicrosoftExternalException(
                $"Microsoft Graph document download failed with status {(int)response.StatusCode}.",
                statusCode: response.StatusCode);
        }

        if (response.Content.Headers.ContentLength > maximumBytes)
        {
            throw new MicrosoftExternalException("Microsoft Graph document exceeds the configured size limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return destination.ToArray();
            }

            if (destination.Length + read > maximumBytes)
            {
                throw new MicrosoftExternalException("Microsoft Graph document exceeds the configured size limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}
