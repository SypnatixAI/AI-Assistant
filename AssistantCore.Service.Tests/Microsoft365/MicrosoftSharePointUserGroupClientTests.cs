using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftSharePointUserGroupClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_SharePointGroups_When_GetGroupIdsAsync_Then_ReturnsIdentifiers(
        string accessToken)
    {
        // Given
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"value":[{"Id":5},{"Id":3},{"Id":5}]}
                    """)
            };
        }));
        var client = new MicrosoftSharePointUserGroupClient(httpClient);

        // When
        var result = await client.GetGroupIdsAsync(
            "https://contoso.sharepoint.com/sites/finance",
            accessToken,
            "ada@contoso.com",
            CancellationToken.None);

        // Then
        Assert.Equal(["3", "5"], result);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal(accessToken, capturedRequest.Headers.Authorization?.Parameter);
        Assert.Contains(
            "siteusers/getbyemail('ada%40contoso.com')/groups",
            capturedRequest.RequestUri!.AbsoluteUri,
            StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_UserIsNotKnownBySharePointSite_When_GetGroupIdsAsync_Then_ReturnsNoIdentifiers(
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var client = new MicrosoftSharePointUserGroupClient(httpClient);

        // When
        var result = await client.GetGroupIdsAsync(
            "https://contoso.sharepoint.com/sites/finance",
            accessToken,
            "ada@contoso.com",
            CancellationToken.None);

        // Then
        Assert.Empty(result);
    }

    [Theory, AutoDomainData]
    public async Task Given_CrossOriginContinuation_When_GetGroupIdsAsync_Then_RejectsResponse(
        string accessToken)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "value":[{"Id":5}],
                      "@odata.nextLink":"https://attacker.example/_api/groups"
                    }
                    """)
            }));
        var client = new MicrosoftSharePointUserGroupClient(httpClient);

        // When
        var action = () => client.GetGroupIdsAsync(
            "https://contoso.sharepoint.com/sites/finance",
            accessToken,
            "ada@contoso.com",
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<MicrosoftExternalException>(action);
    }
}
