using System.Net;
using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphSiteClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_MultipleSitePages_When_ListAsync_Then_ReturnsEverySiteWithBearerAuthorization(
        string firstSiteId,
        string secondSiteId,
        string accessToken)
    {
        // Given
        var authorizations = new List<AuthenticationHeaderValue?>();
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            authorizations.Add(request.Headers.Authorization);
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(requestCount == 1
                    ? $$"""{"value":[{"id":"{{firstSiteId}}","displayName":"Finance","webUrl":"https://contoso.sharepoint.com/sites/finance"}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/sites?$skiptoken=next"}"""
                    : $$"""{"value":[{"id":"{{secondSiteId}}","displayName":"Operations","webUrl":"https://contoso.sharepoint.com/sites/operations"}]}""")
            };
        }));
        var client = new MicrosoftGraphSiteClient(httpClient);

        // When
        var sites = await client.ListAsync(
            "https://graph.microsoft.com",
            accessToken,
            CancellationToken.None);

        // Then
        Assert.Collection(
            sites,
            site => Assert.Equal(firstSiteId, site.SiteId),
            site => Assert.Equal(secondSiteId, site.SiteId));
        Assert.All(authorizations, authorization =>
        {
            Assert.Equal("Bearer", authorization?.Scheme);
            Assert.Equal(accessToken, authorization?.Parameter);
        });
    }

    [Theory, AutoDomainData]
    public async Task Given_AnEmptySiteListing_When_ListAsync_Then_RetriesWithSearchWildcard(
        string fallbackSiteId,
        string accessToken)
    {
        // Given
        var requestUris = new List<Uri?>();
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUris.Add(request.RequestUri);
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(requestCount == 1
                    ? """{"value":[]}"""
                    : $$"""{"value":[{"id":"{{fallbackSiteId}}","displayName":"Fallback","webUrl":"https://contoso.sharepoint.com/sites/fallback"}]}""")
            };
        }));
        var client = new MicrosoftGraphSiteClient(httpClient);

        // When
        var sites = await client.ListAsync(
            "https://graph.microsoft.com",
            accessToken,
            CancellationToken.None);

        // Then
        var site = Assert.Single(sites);
        Assert.Equal(fallbackSiteId, site.SiteId);
        Assert.Equal(2, requestUris.Count);
        Assert.Contains("/v1.0/sites?$select=id,displayName,webUrl", requestUris[0]!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("/v1.0/sites?search=*&$select=id,displayName,webUrl", requestUris[1]!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory, InlineAutoDomainData("https://untrusted.example/v1.0/sites")]
    public async Task Given_AnUntrustedNextLink_When_ListAsync_Then_RejectsPaginationUrl(
        string nextLink,
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"value":[],"@odata.nextLink":"{{nextLink}}"}""")
            }));
        var client = new MicrosoftGraphSiteClient(httpClient);

        // When
        var action = () => client.ListAsync(
            "https://graph.microsoft.com",
            accessToken,
            CancellationToken.None);

        // Then
        var exception = await Assert.ThrowsAsync<MicrosoftExternalException>(action);
        Assert.Contains("untrusted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
