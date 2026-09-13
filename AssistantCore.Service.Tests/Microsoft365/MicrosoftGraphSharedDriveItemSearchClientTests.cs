using System.Net;
using System.Text;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphSharedDriveItemSearchClientTests
{
    [Fact]
    public async Task Given_OwnAndRemoteItems_When_SearchingSharedContent_Then_ReturnsOnlyCanonicalRemoteItems()
    {
        // Given
        const string payload = """
        {
          "value": [
            {
              "id": "local-item",
              "name": "Local.xlsx",
              "eTag": "local-etag",
              "webUrl": "https://contoso-my.sharepoint.com/local",
              "file": { "mimeType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }
            },
            {
              "id": "shortcut-item",
              "name": "Budget-2027.xlsx",
              "remoteItem": {
                "id": "budget-item",
                "name": "Budget-2027.xlsx",
                "eTag": "budget-etag",
                "webUrl": "https://contoso-my.sharepoint.com/personal/alice/Budget-2027.xlsx",
                "size": 1234,
                "file": { "mimeType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                "parentReference": { "driveId": "alice-owner-drive" }
              }
            }
          ]
        }
        """;
        var handler = new RecordingHandler(_ => JsonResponse(payload));
        var client = new MicrosoftGraphSharedDriveItemSearchClient(new HttpClient(handler));

        // When
        var results = new List<AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftDriveItemDelta>();
        await foreach (var driveItem in client.GetSharedItemsAsync(
                           "https://graph.microsoft.com/",
                           "secret-access-token",
                           "bob-drive"))
        {
            results.Add(driveItem);
        }

        // Then
        var remoteItem = Assert.Single(results);
        Assert.Equal("budget-item", remoteItem.Id);
        Assert.Equal("alice-owner-drive", remoteItem.CanonicalDriveId);
        Assert.Equal("Budget-2027.xlsx", remoteItem.Name);
        Assert.Equal("budget-etag", remoteItem.ETag);
        Assert.Equal("https://contoso-my.sharepoint.com/personal/alice/Budget-2027.xlsx", remoteItem.WebUrl);
        Assert.True(remoteItem.IsFile);
        Assert.False(remoteItem.IsFolder);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            remoteItem.MimeType);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("secret-access-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.Contains("/v1.0/drives/bob-drive/search(q='')", handler.LastRequest?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task Given_PaginatedSharedItems_When_Searching_Then_FollowsTrustedNextLink()
    {
        // Given
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse("""
            {
              "value": [
                {
                  "id": "shortcut-1",
                  "remoteItem": {
                    "id": "item-1",
                    "name": "One.pdf",
                    "eTag": "etag-1",
                    "webUrl": "https://contoso/one.pdf",
                    "file": { "mimeType": "application/pdf" },
                    "parentReference": { "driveId": "owner-drive" }
                  }
                }
              ],
              "@odata.nextLink": "https://graph.microsoft.com/v1.0/drives/bob-drive/search(q='')?$skiptoken=opaque"
            }
            """),
            JsonResponse("""
            {
              "value": [
                {
                  "id": "shortcut-2",
                  "remoteItem": {
                    "id": "item-2",
                    "name": "Two.pdf",
                    "eTag": "etag-2",
                    "webUrl": "https://contoso/two.pdf",
                    "file": { "mimeType": "application/pdf" },
                    "parentReference": { "driveId": "owner-drive" }
                  }
                }
              ]
            }
            """)
        ]);
        var handler = new RecordingHandler(_ => responses.Dequeue());
        var client = new MicrosoftGraphSharedDriveItemSearchClient(new HttpClient(handler));

        // When
        var results = new List<AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftDriveItemDelta>();
        await foreach (var item in client.GetSharedItemsAsync(
                           "https://graph.microsoft.com/",
                           "token",
                           "bob-drive"))
        {
            results.Add(item);
        }

        // Then
        Assert.Equal(2, results.Count);
        Assert.Equal(["item-1", "item-2"], results.Select(item => item.Id).ToArray());
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Given_UntrustedPaginationUrl_When_Searching_Then_RejectsTheResponse()
    {
        // Given
        var handler = new RecordingHandler(_ => JsonResponse("""
        {
          "value": [],
          "@odata.nextLink": "https://attacker.example/steal-token"
        }
        """));
        var client = new MicrosoftGraphSharedDriveItemSearchClient(new HttpClient(handler));

        // When
        async Task Act()
        {
            await foreach (var _ in client.GetSharedItemsAsync(
                               "https://graph.microsoft.com/",
                               "secret-access-token",
                               "bob-drive"))
            {
            }
        }

        // Then
        var exception = await Assert.ThrowsAsync<MicrosoftExternalException>(Act);
        Assert.Contains("pagination URL was not trusted", exception.Message);
        Assert.Equal(1, handler.RequestCount);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
