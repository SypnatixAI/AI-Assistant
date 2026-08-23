using System.Net;
using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphListSchemaClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_MultipleColumnPages_When_GetColumnsAsync_Then_ReturnsEveryColumnWithBearerAuthorization(
        string firstColumnId,
        string secondColumnId,
        string accessToken)
    {
        // Given
        var requestUris = new List<Uri>();
        var authorizations = new List<AuthenticationHeaderValue?>();
        var requestCount = 0;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUris.Add(request.RequestUri!);
            authorizations.Add(request.Headers.Authorization);
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(requestCount == 1
                    ? $"{{\"value\":[{{\"id\":\"{firstColumnId}\",\"name\":\"Title\",\"text\":{{}}}}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/sites/site/lists/list/columns?$skiptoken=next\"}}"
                    : $"{{\"value\":[{{\"id\":\"{secondColumnId}\",\"name\":\"Status\",\"choice\":{{\"choices\":[\"Open\",\"Closed\"]}}}}]}}")
            };
        }));
        var client = new MicrosoftGraphListSchemaClient(httpClient);

        // When
        var columns = await client.GetColumnsAsync(
            "https://graph.microsoft.com",
            accessToken,
            "contoso.sharepoint.com,site/id,web-id",
            "list/id",
            CancellationToken.None);

        // Then
        Assert.Collection(
            columns,
            column => Assert.Equal(firstColumnId, column.Id),
            column => Assert.Equal(secondColumnId, column.Id));
        Assert.Equal(2, requestCount);
        Assert.Equal(
            "/v1.0/sites/contoso.sharepoint.com%2Csite%2Fid%2Cweb-id/lists/list%2Fid/columns",
            requestUris[0].AbsolutePath);
        Assert.All(authorizations, authorization =>
        {
            Assert.Equal("Bearer", authorization?.Scheme);
            Assert.Equal(accessToken, authorization?.Parameter);
        });
    }

    [Theory, InlineAutoDomainData("https://untrusted.example/v1.0/sites/site/lists/list/columns")]
    public async Task Given_AnUntrustedNextLink_When_GetColumnsAsync_Then_RejectsPaginationUrl(
        string nextLink,
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"value\":[],\"@odata.nextLink\":\"{nextLink}\"}}")
            }));
        var client = new MicrosoftGraphListSchemaClient(httpClient);

        // When
        var action = () => client.GetColumnsAsync(
            "https://graph.microsoft.com",
            accessToken,
            "site-id",
            "list-id",
            CancellationToken.None);

        // Then
        var exception = await Assert.ThrowsAsync<MicrosoftExternalException>(action);
        Assert.Contains("not trusted", exception.Message, StringComparison.Ordinal);
    }
}
