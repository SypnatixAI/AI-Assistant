using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftIdentityClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_ATenantAndClientCredentials_When_AcquireApplicationTokenAsync_Then_UsesClientCredentialsGrant(
        string tenantId,
        string clientId,
        string clientSecret,
        string accessToken)
    {
        // Given
        Uri? requestUri = null;
        string? body = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"access_token":"{{accessToken}}","expires_in":3600}""")
            };
        }));
        var client = new MicrosoftIdentityClient(httpClient);

        // When
        var result = await client.AcquireApplicationTokenAsync(
            "https://login.microsoftonline.com",
            tenantId,
            clientId,
            clientSecret,
            CancellationToken.None);

        // Then
        Assert.Contains(Uri.EscapeDataString(tenantId), requestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("grant_type=client_credentials", body, StringComparison.Ordinal);
        Assert.Contains($"client_id={Uri.EscapeDataString(clientId)}", body, StringComparison.Ordinal);
        Assert.Equal(accessToken, result.AccessToken);
    }

    [Theory, AutoDomainData]
    public void Given_ConsentState_When_CreateAdminConsentUri_Then_UsesMultitenantAdminConsentFlow(
        string clientId,
        string state)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new MicrosoftIdentityClient(httpClient);

        // When
        var uri = client.CreateAdminConsentUri(
            "https://login.microsoftonline.com",
            clientId,
            "https://localhost/callback",
            state);

        // Then
        Assert.Equal("login.microsoftonline.com", uri.Host);
        Assert.Contains("/organizations/v2.0/adminconsent", uri.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain("response_type", uri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt", uri.Query, StringComparison.Ordinal);
        Assert.Contains(
            Uri.EscapeDataString("https://graph.microsoft.com/.default"),
            uri.Query,
            StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(state), uri.Query, StringComparison.Ordinal);
    }
}

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(responseFactory(request));
}
