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
    public void Given_ConsentState_When_CreateAuthorizationUri_Then_UsesMultitenantAuthorizationCodeFlow(
        string clientId,
        string state)
    {
        // Given
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new MicrosoftIdentityClient(httpClient);

        // When
        var uri = client.CreateAuthorizationUri(
            "https://login.microsoftonline.com",
            clientId,
            "https://localhost/callback",
            state);

        // Then
        Assert.Equal("login.microsoftonline.com", uri.Host);
        Assert.Contains("/organizations/oauth2/v2.0/authorize", uri.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("response_type=code", uri.Query, StringComparison.Ordinal);
        Assert.Contains("prompt=admin_consent", uri.Query, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(state), uri.Query, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_AuthorizationCode_When_ExchangeAuthorizationCodeAsync_Then_ReturnsTokenWithoutLoggingIt(
        string accessToken)
    {
        // Given
        HttpRequestMessage? receivedRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            receivedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"access_token":"{{accessToken}}","expires_in":3600}""")
            };
        }));
        var client = new MicrosoftIdentityClient(httpClient);

        // When
        var result = await client.ExchangeAuthorizationCodeAsync(
            "https://login.microsoftonline.com",
            "client-id",
            "client-secret",
            "https://localhost/callback",
            "authorization-code",
            CancellationToken.None);

        // Then
        Assert.Equal(accessToken, result.AccessToken);
        Assert.Equal(3600, result.ExpiresInSeconds);
        Assert.Equal(HttpMethod.Post, receivedRequest?.Method);
        Assert.Contains("/organizations/oauth2/v2.0/token", receivedRequest?.RequestUri?.AbsolutePath);
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
