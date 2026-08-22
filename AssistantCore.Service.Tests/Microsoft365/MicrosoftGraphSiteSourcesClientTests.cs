using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphSiteSourcesClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_MultipleDrivePages_When_GetSiteDrivesAsync_Then_ReturnsEveryDriveWithBearerAuthorization(
        string firstDriveId,
        string secondDriveId,
        string accessToken)
    {
        // Given
        var authorizations = new List<AuthenticationHeaderValue?>();
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            authorizations.Add(request.Headers.Authorization);
            requestCount++;
            return requestCount == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"value":[{"id":"{{firstDriveId}}","name":"Documents","webUrl":"https://contoso.sharepoint.com/Documents","driveType":"documentLibrary"}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/sites/site-id/drives?$skiptoken=next"}""")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"value":[{"id":"{{secondDriveId}}","name":"Archives","webUrl":"https://contoso.sharepoint.com/Archives","driveType":"documentLibrary"}]}""")
                };
        }));
        var client = new MicrosoftGraphSiteSourcesClient(httpClient);

        // When
        var result = await client.GetSiteDrivesAsync(
            "https://graph.microsoft.com",
            accessToken,
            "contoso.sharepoint.com,site-id,web-id",
            CancellationToken.None);

        // Then
        Assert.Collection(
            result,
            drive => Assert.Equal(firstDriveId, drive.Id),
            drive => Assert.Equal(secondDriveId, drive.Id));
        Assert.Equal(2, requestCount);
        Assert.All(authorizations, authorization =>
        {
            Assert.Equal("Bearer", authorization?.Scheme);
            Assert.Equal(accessToken, authorization?.Parameter);
        });
    }

    [Theory, AutoDomainData]
    public async Task Given_AListPage_When_GetSiteListsAsync_Then_MapsListInformation(
        string listId,
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        value = new[]
                        {
                            new
                            {
                                id = listId,
                                displayName = "Demandes d'achat",
                                webUrl = "https://contoso.sharepoint.com/Lists/Achats",
                                list = new { hidden = true, template = "genericList" }
                            }
                        }
                    }))
            }));
        var client = new MicrosoftGraphSiteSourcesClient(httpClient);

        // When
        var result = await client.GetSiteListsAsync(
            "https://graph.microsoft.com",
            accessToken,
            "contoso.sharepoint.com,site-id,web-id",
            CancellationToken.None);

        // Then
        var list = Assert.Single(result);
        Assert.Equal(listId, list.Id);
        Assert.Equal("Demandes d'achat", list.DisplayName);
        Assert.Equal("https://contoso.sharepoint.com/Lists/Achats", list.WebUrl);
        Assert.True(list.IsHidden);
        Assert.Equal("genericList", list.Template);
        Assert.False(list.IsSystem);
        Assert.False(list.IsDeleted);
    }

    [Theory, InlineAutoDomainData("https://untrusted.example/v1.0/sites/site-id/lists")]
    public async Task Given_AnUntrustedNextLink_When_GetSiteListsAsync_Then_RejectsPaginationUrl(
        string nextLink,
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"value":[],"@odata.nextLink":"{{nextLink}}"}""")
            }));
        var client = new MicrosoftGraphSiteSourcesClient(httpClient);

        // When
        var action = () => client.GetSiteListsAsync(
            "https://graph.microsoft.com",
            accessToken,
            "site-id",
            CancellationToken.None);

        // Then
        var exception = await Assert.ThrowsAsync<MicrosoftExternalException>(action);
        Assert.Contains("not trusted", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_ARepeatedNextLink_When_GetSiteDrivesAsync_Then_RejectsPaginationLoop(
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"value":[],"@odata.nextLink":"{{request.RequestUri}}"}""")
            }));
        var client = new MicrosoftGraphSiteSourcesClient(httpClient);

        // When
        var action = () => client.GetSiteDrivesAsync(
            "https://graph.microsoft.com",
            accessToken,
            "site-id",
            CancellationToken.None);

        // Then
        var exception = await Assert.ThrowsAsync<MicrosoftExternalException>(action);
        Assert.Contains("loop", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineAutoDomainData("{\"value\":null}")]
    [InlineAutoDomainData("{invalid-json")]
    public async Task Given_AnInvalidCollectionResponse_When_GetSiteListsAsync_Then_ThrowsExternalException(
        string responseBody,
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            }));
        var client = new MicrosoftGraphSiteSourcesClient(httpClient);

        // When
        var action = () => client.GetSiteListsAsync(
            "https://graph.microsoft.com",
            accessToken,
            "site-id",
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<MicrosoftExternalException>(action);
    }
}
