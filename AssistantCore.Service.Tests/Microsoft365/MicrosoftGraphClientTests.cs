using System.Net;
using System.Net.Http.Headers;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftGraphClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_AccessToken_When_GetCurrentTenantAsync_Then_ReturnsTenantIdentifiedByGraph(
        string tenantId,
        string displayName,
        string accessToken)
    {
        // Given
        AuthenticationHeaderValue? authorization = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            authorization = request.Headers.Authorization;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"value":[{"id":"{{tenantId}}","displayName":"{{displayName}}"}]}""")
            };
        }));
        var client = new MicrosoftGraphClient(httpClient);

        // When
        var result = await client.GetCurrentTenantAsync(
            "https://graph.microsoft.com",
            accessToken,
            CancellationToken.None);

        // Then
        Assert.Equal(tenantId, result.Id);
        Assert.Equal(displayName, result.DisplayName);
        Assert.Equal("Bearer", authorization?.Scheme);
        Assert.Equal(accessToken, authorization?.Parameter);
    }
}
