using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphDriveItemDeltaClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_PagedDriveDelta_When_GetInitialPagesAsync_Then_StreamsFacetsAndPreservesOpaqueDeltaLink(
        string accessToken)
    {
        // Given
        const string deltaLink = "https://graph.microsoft.com/v1.0/drives/drive/root/delta?token=opaque%2Bvalue%3D%3D";
        var requestUris = new List<Uri>();
        var preferHeaders = new List<string>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUris.Add(request.RequestUri!);
            preferHeaders.Add(string.Join(",", request.Headers.GetValues("Prefer")));
            return requestUris.Count == 1
                ? CreateResponse("{\"value\":[{\"id\":\"file-1\",\"name\":\"report.pdf\",\"eTag\":\"etag-1\",\"size\":42,\"file\":{\"mimeType\":\"application/pdf\"}}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/drives/drive/root/delta?$skiptoken=page-2\"}")
                : CreateResponse($"{{\"value\":[{{\"id\":\"folder-1\",\"folder\":{{}},\"deleted\":{{\"state\":\"deleted\"}}}}],\"@odata.deltaLink\":\"{deltaLink}\"}}");
        }));
        var client = new MicrosoftGraphDriveItemDeltaClient(httpClient);
        await using var enumerator = client.GetInitialPagesAsync(
                "https://graph.microsoft.com",
                accessToken,
                "drive/id",
                CancellationToken.None)
            .GetAsyncEnumerator();

        // When
        Assert.Empty(requestUris);
        Assert.True(await enumerator.MoveNextAsync());
        var filePage = enumerator.Current;
        Assert.Single(requestUris);
        Assert.True(await enumerator.MoveNextAsync());
        var deletedPage = enumerator.Current;
        Assert.False(await enumerator.MoveNextAsync());

        // Then
        var file = Assert.Single(filePage.Items);
        Assert.True(file.IsFile);
        Assert.Equal("application/pdf", file.MimeType);
        Assert.Equal(42, file.Size);
        var deletedFolder = Assert.Single(deletedPage.Items);
        Assert.True(deletedFolder.IsDeleted);
        Assert.True(deletedFolder.IsFolder);
        Assert.Equal(deltaLink, deletedPage.DeltaLink);
        Assert.Equal("/v1.0/drives/drive%2Fid/root/delta", requestUris[0].AbsolutePath);
        Assert.All(preferHeaders, header =>
        {
            Assert.Contains("hierarchicalsharing", header, StringComparison.Ordinal);
            Assert.Contains("deltashowremovedasdeleted", header, StringComparison.Ordinal);
            Assert.Contains("deltatraversepermissiongaps", header, StringComparison.Ordinal);
            Assert.Contains("deltashowsharingchanges", header, StringComparison.Ordinal);
        });
    }

    [Theory, AutoDomainData]
    public async Task Given_AStoredOpaqueDriveDeltaLink_When_GetDeltaPagesAsync_Then_RequestsTheExactLink(
        string accessToken)
    {
        // Given
        const string storedDeltaLink = "https://graph.microsoft.com/v1.0/drives/drive/root/delta?token=opaque%2Bvalue%3D%3D&custom=value";
        const string nextDeltaLink = "https://graph.microsoft.com/v1.0/drives/drive/root/delta?token=next";
        Uri? requestUri = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return CreateResponse($"{{\"value\":[],\"@odata.deltaLink\":\"{nextDeltaLink}\"}}");
        }));
        var client = new MicrosoftGraphDriveItemDeltaClient(httpClient);

        // When
        await foreach (var _ in client.GetDeltaPagesAsync(
                           "https://graph.microsoft.com",
                           accessToken,
                           storedDeltaLink,
                           CancellationToken.None))
        {
        }

        // Then
        Assert.Equal(storedDeltaLink, requestUri?.OriginalString);
    }

    private static HttpResponseMessage CreateResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };
}
