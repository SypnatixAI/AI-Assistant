using System.Net;
using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphListItemDeltaClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_ThreeDeltaPages_When_GetInitialPagesAsync_Then_RequestsEachPageLazilyAndMapsItems(
        string activeItemId,
        string deletedItemId,
        string eTag,
        string accessToken)
    {
        // Given
        const string deltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=opaque%2Bvalue%3D%3D";
        var requestCount = 0;
        var requestUris = new List<Uri>();
        var authorizations = new List<AuthenticationHeaderValue?>();
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestCount++;
            requestUris.Add(request.RequestUri!);
            authorizations.Add(request.Headers.Authorization);
            return requestCount switch
            {
                1 => CreateResponse($"{{\"value\":[{{\"id\":\"{activeItemId}\",\"eTag\":\"{eTag}\",\"createdDateTime\":\"2026-08-01T10:00:00Z\",\"lastModifiedDateTime\":\"2026-08-02T11:00:00Z\",\"webUrl\":\"https://contoso/items/1\",\"fields\":{{\"Title\":\"Request\"}}}}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$skiptoken=page-2\"}}"),
                2 => CreateResponse($"{{\"value\":[{{\"id\":\"{deletedItemId}\",\"deleted\":{{\"state\":\"deleted\"}}}}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$skiptoken=page-3\"}}"),
                _ => CreateResponse($"{{\"value\":[],\"@odata.deltaLink\":\"{deltaLink}\"}}")
            };
        }));
        var client = new MicrosoftGraphListItemDeltaClient(httpClient);
        await using var enumerator = client.GetInitialPagesAsync(
                "https://graph.microsoft.com",
                accessToken,
                "contoso.sharepoint.com,site-id,web-id",
                "list-id",
                CancellationToken.None)
            .GetAsyncEnumerator();

        // When
        Assert.Equal(0, requestCount);
        Assert.True(await enumerator.MoveNextAsync());
        var firstPage = enumerator.Current;
        Assert.Equal(1, requestCount);
        Assert.True(await enumerator.MoveNextAsync());
        var secondPage = enumerator.Current;
        Assert.Equal(2, requestCount);
        Assert.True(await enumerator.MoveNextAsync());
        var finalPage = enumerator.Current;
        Assert.Equal(3, requestCount);
        Assert.False(await enumerator.MoveNextAsync());

        // Then
        var activeItem = Assert.Single(firstPage.Items);
        Assert.Equal(activeItemId, activeItem.Id);
        Assert.Equal(eTag, activeItem.ETag);
        Assert.Equal("Request", activeItem.Fields?.GetProperty("Title").GetString());
        Assert.False(activeItem.IsDeleted);
        var deletedItem = Assert.Single(secondPage.Items);
        Assert.Equal(deletedItemId, deletedItem.Id);
        Assert.True(deletedItem.IsDeleted);
        Assert.Null(deletedItem.ETag);
        Assert.Null(deletedItem.Fields);
        Assert.Equal(deltaLink, finalPage.DeltaLink);
        Assert.Contains("$expand=fields", requestUris[0].Query, StringComparison.Ordinal);
        Assert.All(authorizations, authorization =>
        {
            Assert.Equal("Bearer", authorization?.Scheme);
            Assert.Equal(accessToken, authorization?.Parameter);
        });
    }

    [Theory, AutoDomainData]
    public async Task Given_SecondDeltaPageRetriesExhausted_When_GetInitialPagesAsync_Then_FirstPageWasAlreadyYielded(
        string itemId,
        string eTag,
        string accessToken)
    {
        // Given
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return requestCount == 1
                ? CreateResponse($"{{\"value\":[{{\"id\":\"{itemId}\",\"eTag\":\"{eTag}\",\"fields\":{{}}}}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$skiptoken=page-2\"}}")
                : new HttpResponseMessage(HttpStatusCode.BadGateway);
        }));
        var client = new MicrosoftGraphListItemDeltaClient(httpClient);
        await using var enumerator = client.GetInitialPagesAsync(
                "https://graph.microsoft.com",
                accessToken,
                "site-id",
                "list-id",
                CancellationToken.None)
            .GetAsyncEnumerator();

        // When
        var firstPageAvailable = await enumerator.MoveNextAsync();
        var action = async () => await enumerator.MoveNextAsync();

        // Then
        Assert.True(firstPageAvailable);
        Assert.Single(enumerator.Current.Items);
        await Assert.ThrowsAsync<MicrosoftExternalException>(action);
        Assert.Equal(5, requestCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_TooManyRequestsWithRetryAfter_When_GetInitialPagesAsync_Then_RetriesBeforeReturningPage(
        string accessToken)
    {
        // Given
        const string deltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=next";
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"error\":{\"code\":\"tooManyRequests\"}}")
                };
                throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return throttled;
            }

            return CreateResponse($"{{\"value\":[],\"@odata.deltaLink\":\"{deltaLink}\"}}");
        }));
        var client = new MicrosoftGraphListItemDeltaClient(httpClient);

        // When
        var pages = new List<AssistantCore.ExternalServices.Entities.Microsoft.MicrosoftListItemDeltaPage>();
        await foreach (var page in client.GetInitialPagesAsync(
                           "https://graph.microsoft.com",
                           accessToken,
                           "site-id",
                           "list-id",
                           CancellationToken.None))
        {
            pages.Add(page);
        }

        // Then
        Assert.Equal(2, requestCount);
        Assert.Equal(deltaLink, Assert.Single(pages).DeltaLink);
    }

    [Theory, AutoDomainData]
    public async Task Given_ServerErrors_When_GetInitialPagesAsync_Then_RetriesWithBoundedBackoff(
        string accessToken)
    {
        // Given
        const string deltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=next";
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return requestCount < 3
                ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                : CreateResponse($"{{\"value\":[],\"@odata.deltaLink\":\"{deltaLink}\"}}");
        }));
        var client = new MicrosoftGraphListItemDeltaClient(httpClient);

        // When
        await foreach (var _ in client.GetInitialPagesAsync(
                           "https://graph.microsoft.com",
                           accessToken,
                           "site-id",
                           "list-id",
                           CancellationToken.None))
        {
        }

        // Then
        Assert.Equal(3, requestCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AStoredOpaqueDeltaLink_When_GetDeltaPagesAsync_Then_RequestsTheExactLink(
        string accessToken)
    {
        // Given
        const string storedDeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=opaque%2Bvalue%3D%3D&custom=value";
        const string nextDeltaLink = "https://graph.microsoft.com/v1.0/sites/site/lists/list/items/delta?$deltatoken=next";
        Uri? requestUri = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return CreateResponse($"{{\"value\":[],\"@odata.deltaLink\":\"{nextDeltaLink}\"}}");
        }));
        var client = new MicrosoftGraphListItemDeltaClient(httpClient);

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
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
}
