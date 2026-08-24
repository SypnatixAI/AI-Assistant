using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftIdentityClientSharePointTokenTests
{
    [Theory, AutoDomainData]
    public async Task Given_ASharePointScope_When_AcquireApplicationTokenForScopeAsync_Then_RequestsATokenForTheSharePointHost(
        string tenantId,
        string clientId,
        string clientSecret,
        string accessToken)
    {
        // Given
        const string sharePointScope = "https://contoso.sharepoint.com/.default";
        string? requestBody = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"access_token":"{{accessToken}}","expires_in":3600}""")
            };
        }));
        var client = new MicrosoftIdentityClient(httpClient);

        // When
        var result = await client.AcquireApplicationTokenForScopeAsync(
            "https://login.microsoftonline.com",
            tenantId,
            clientId,
            clientSecret,
            sharePointScope,
            CancellationToken.None);

        // Then
        Assert.Contains(
            $"scope={Uri.EscapeDataString(sharePointScope)}",
            requestBody,
            StringComparison.Ordinal);
        Assert.Equal(accessToken, result.AccessToken);
    }
}
