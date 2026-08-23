using System.Net;
using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Infrastructure.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SiteSourcesClientAdapterTests
{
    [Theory, AutoDomainData]
    public async Task Given_GraphDrivesAndLists_When_GetSiteSourcesAsync_Then_FiltersListsAndReturnsInactiveDiscoveredSources(
        string accessToken,
        string siteId)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            request.RequestUri?.AbsolutePath.EndsWith("/drives", StringComparison.Ordinal) == true
                ? CreateResponse(
                    """{"value":[{"id":"drive-id","name":"Documents","webUrl":"https://contoso.sharepoint.com/Documents","driveType":"documentLibrary"}]}""")
                : CreateResponse(
                    """{"value":[{"id":"document-list","displayName":"Documents","list":{"hidden":false,"template":"documentLibrary"}},{"id":"hidden-list","displayName":"Hidden","list":{"hidden":true,"template":"genericList"}},{"id":"system-list","displayName":"System","system":{},"list":{"hidden":false,"template":"genericList"}},{"id":"deleted-list","displayName":"Deleted","deleted":{},"list":{"hidden":false,"template":"genericList"}},{"id":"content-list","displayName":"Requests","webUrl":"https://contoso.sharepoint.com/Lists/Requests","list":{"hidden":false,"template":"genericList"}}]}""")));
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.GetSiteSourcesAsync(
            accessToken,
            siteId,
            CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365SiteSourcesDiscoveryStatus.Succeeded, result.Status);
        var drive = Assert.Single(result.Sources.Drives);
        Assert.Equal(siteId, drive.SiteId);
        Assert.Equal("drive-id", drive.DriveId);
        Assert.Equal(Microsoft365SourceStatus.Discovered, drive.Status);
        Assert.False(drive.IsIndexed);

        var list = Assert.Single(result.Sources.Lists);
        Assert.Equal(siteId, list.SiteId);
        Assert.Equal("content-list", list.ListId);
        Assert.Equal("Requests", list.DisplayName);
        Assert.Equal(Microsoft365SourceStatus.Discovered, list.Status);
        Assert.False(list.IsIndexed);
    }

    [Theory]
    [InlineAutoDomainData(HttpStatusCode.Forbidden, Microsoft365SiteSourcesDiscoveryStatus.Forbidden)]
    [InlineAutoDomainData(HttpStatusCode.NotFound, Microsoft365SiteSourcesDiscoveryStatus.SiteNotFound)]
    public async Task Given_AnExpectedGraphFailure_When_GetSiteSourcesAsync_Then_ReturnsApplicationStatus(
        HttpStatusCode graphStatus,
        Microsoft365SiteSourcesDiscoveryStatus expectedStatus,
        string accessToken,
        string siteId)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(graphStatus)));
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.GetSiteSourcesAsync(
            accessToken,
            siteId,
            CancellationToken.None);

        // Then
        Assert.Equal(expectedStatus, result.Status);
        Assert.Empty(result.Sources.Drives);
        Assert.Empty(result.Sources.Lists);
        Assert.Null(result.RetryAfterDelay);
        Assert.Null(result.RetryAfterAt);
    }

    [Theory, InlineAutoDomainData(0)]
    public async Task Given_GraphThrottlingWithDelay_When_GetSiteSourcesAsync_Then_ReturnsRetryDelay(
        int retryAfterSeconds,
        string accessToken,
        string siteId)
    {
        // Given
        var retryAfter = TimeSpan.FromSeconds(retryAfterSeconds);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            CreateThrottledResponse(new RetryConditionHeaderValue(retryAfter))));
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.GetSiteSourcesAsync(
            accessToken,
            siteId,
            CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365SiteSourcesDiscoveryStatus.Throttled, result.Status);
        Assert.Equal(retryAfter, result.RetryAfterDelay);
        Assert.Null(result.RetryAfterAt);
    }

    [Theory, InlineAutoDomainData(0)]
    public async Task Given_GraphThrottlingWithDate_When_GetSiteSourcesAsync_Then_ReturnsRetryDate(
        int secondsAfterUnixEpoch,
        string accessToken,
        string siteId)
    {
        // Given
        var retryAfterAt = DateTimeOffset.UnixEpoch.AddSeconds(secondsAfterUnixEpoch);
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            CreateThrottledResponse(new RetryConditionHeaderValue(retryAfterAt))));
        var adapter = CreateAdapter(httpClient);

        // When
        var result = await adapter.GetSiteSourcesAsync(
            accessToken,
            siteId,
            CancellationToken.None);

        // Then
        Assert.Equal(Microsoft365SiteSourcesDiscoveryStatus.Throttled, result.Status);
        Assert.Null(result.RetryAfterDelay);
        Assert.Equal(retryAfterAt, result.RetryAfterAt);
    }

    [Theory, AutoDomainData]
    public async Task Given_CancelledDiscovery_When_GetSiteSourcesAsync_Then_PropagatesCancellationToken(
        string accessToken,
        string siteId)
    {
        // Given
        var handler = new CancellationRecordingHandler();
        using var httpClient = new HttpClient(handler);
        var adapter = CreateAdapter(httpClient);
        using var cancellationSource = new CancellationTokenSource();

        // When
        var discoveryTask = adapter.GetSiteSourcesAsync(
            accessToken,
            siteId,
            cancellationSource.Token);
        await cancellationSource.CancelAsync();

        // Then
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => discoveryTask);
        Assert.True(handler.ReceivedCancellationToken.IsCancellationRequested);
    }

    [Theory, AutoDomainData]
    public void Given_DiscoveredSourceModels_When_InspectingProperties_Then_OrganizationIsNotExposed(
        Microsoft365DiscoveredDrive drive,
        Microsoft365DiscoveredList list)
    {
        // Given
        var sourceTypes = new[] { drive.GetType(), list.GetType() };

        // When
        var organizationProperties = sourceTypes
            .SelectMany(type => type.GetProperties())
            .Where(property => property.Name.Contains("Organization", StringComparison.Ordinal))
            .ToArray();

        // Then
        Assert.Empty(organizationProperties);
    }

    [Theory, AutoDomainData]
    public async Task Given_GraphFailure_When_GetSiteSourcesAsync_Then_ThrowsApplicationExternalException(
        string accessToken,
        string siteId)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var adapter = CreateAdapter(httpClient);

        // When
        var action = () => adapter.GetSiteSourcesAsync(
            accessToken,
            siteId,
            CancellationToken.None);

        // Then
        var exception = await Assert.ThrowsAsync<Microsoft365ExternalException>(action);
        Assert.IsType<MicrosoftExternalException>(exception.InnerException);
    }

    private static Microsoft365SiteSourcesClientAdapter CreateAdapter(HttpClient httpClient) =>
        new(
            new MicrosoftGraphSiteSourcesClient(httpClient),
            Options.Create(new Microsoft365Options
            {
                GraphBaseUrl = "https://graph.microsoft.com"
            }));

    private static HttpResponseMessage CreateResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

    private static HttpResponseMessage CreateThrottledResponse(
        RetryConditionHeaderValue retryAfter)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = retryAfter;
        return response;
    }

    private sealed class CancellationRecordingHandler : HttpMessageHandler
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ReceivedCancellationToken = cancellationToken;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
