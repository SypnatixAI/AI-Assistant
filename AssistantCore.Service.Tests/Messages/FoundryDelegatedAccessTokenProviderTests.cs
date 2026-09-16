using System.Net;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Infrastructure.Foundry;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Messages;

public sealed class FoundryDelegatedAccessTokenProviderTests
{
    [Theory, AutoDomainData]
    public async Task Given_EntraUserBearerToken_When_GetAsync_Then_UsesOboForFoundryAudience(
        string accessToken,
        string delegatedToken,
        string clientId,
        string clientSecret)
    {
        // Given
        string? requestBody = null;
        Uri? requestUri = null;
        using var identityHttpClient = new HttpClient(new FoundryTokenStubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"access_token":"{{delegatedToken}}","expires_in":3600}""")
            };
        }));
        var identityClient = new MicrosoftIdentityClient(identityHttpClient);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {accessToken}";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentIdentity = new StubCurrentIdentity
        {
            Identity = new AuthenticatedIdentity(
                IdentityProvider.MicrosoftEntraId,
                "customer-tenant-id",
                "user-id",
                "Test User",
                "test@example.com",
                [])
        };
        var options = Options.Create(new Microsoft365Options
        {
            AuthorityBaseUrl = "https://login.microsoftonline.com",
            ClientId = clientId,
            ClientSecret = clientSecret
        });
        var provider = new FoundryDelegatedAccessTokenProvider(
            accessor,
            currentIdentity,
            identityClient,
            options);

        // When
        var result = await provider.GetAsync(CancellationToken.None);

        // Then
        Assert.Equal(delegatedToken, result.AccessToken);
        Assert.Contains("customer-tenant-id", requestUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", requestBody, StringComparison.Ordinal);
        Assert.Contains($"assertion={Uri.EscapeDataString(accessToken)}", requestBody, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("https://ai.azure.com/.default"), requestBody, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_MissingBearerToken_When_GetAsync_Then_ThrowsUnauthorizedAccessException()
    {
        // Given
        using var identityHttpClient = new HttpClient(new FoundryTokenStubHttpMessageHandler(_ =>
            throw new InvalidOperationException("The identity endpoint must not be called.")));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var provider = new FoundryDelegatedAccessTokenProvider(
            accessor,
            new StubCurrentIdentity(),
            new MicrosoftIdentityClient(identityHttpClient),
            Options.Create(new Microsoft365Options
            {
                AuthorityBaseUrl = "https://login.microsoftonline.com",
                ClientId = "client-id",
                ClientSecret = "client-secret"
            }));

        // When
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            provider.GetAsync(CancellationToken.None));

        // Then
        Assert.Contains("bearer token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class FoundryTokenStubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(responseFactory(request));
}
