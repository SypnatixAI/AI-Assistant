using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Integration;

/// <summary>
/// Verifie que le scope delegue est exige sur les endpoints utilisateur.
/// </summary>
public sealed class RequiredScopeAuthorizationTests
{
    private const string AuthenticateUserRoute = "/api/core/authenticateUser";
    private const string TestAuthenticationScheme = "IntegrationTest";
    private const string ScopeHeaderName = "X-Test-Scp";
    private const string RequiredScope = "access_as_user";

    [Fact]
    public async Task Given_NoToken_When_CallingAuthenticateUser_Then_ReturnsUnauthorized()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: false);
        using var client = CreateClient(factory);

        // When
        using var response = await client.GetAsync(AuthenticateUserRoute);

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Given_AnAuthenticatedTokenWithoutTheRequiredScope_When_CallingAuthenticateUser_Then_ReturnsForbidden()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);

        // When
        using var response = await client.GetAsync(AuthenticateUserRoute);

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Given_AnAuthenticatedTokenWithAnotherScope_When_CallingAuthenticateUser_Then_ReturnsForbidden()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(ScopeHeaderName, "profile openid");

        // When
        using var response = await client.GetAsync(AuthenticateUserRoute);

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Given_AnAuthenticatedTokenWithTheRequiredScope_When_CallingAuthenticateUser_Then_AuthorizationIsGranted()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(ScopeHeaderName, RequiredScope);

        // When
        using var response = await client.GetAsync(AuthenticateUserRoute);
        var body = await response.Content.ReadAsStringAsync();

        // Then
        // Le scope passe : la requete atteint la lecture d'identite, qui refuse ensuite
        // le principal de test parce qu'il ne porte aucun claim de fournisseur.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("fournisseur", body, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool useTestAuthentication)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret"
                        }));

                if (!useTestAuthentication)
                {
                    return;
                }

                builder.ConfigureTestServices(services =>
                    services
                        .AddAuthentication(TestAuthenticationScheme)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            TestAuthenticationScheme,
                            configureOptions: null));
            });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims = Request.Headers.TryGetValue(ScopeHeaderName, out var scopes)
                ? [new Claim("scp", scopes.ToString())]
                : [];

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, TestAuthenticationScheme));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, TestAuthenticationScheme)));
        }
    }
}
