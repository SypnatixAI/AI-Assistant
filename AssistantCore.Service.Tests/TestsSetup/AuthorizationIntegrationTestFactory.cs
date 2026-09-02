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

namespace AssistantCore.Service.Tests;

/// <summary>
/// Cablage partage pour les tests d'integration de la politique d'autorisation par
/// defaut (scope delegue + role d'admission Entra), utilise par les tests du scope
/// et ceux du role d'admission.
/// </summary>
internal static class AuthorizationIntegrationTestFactory
{
    public const string AuthenticateUserRoute = "/api/core/authenticateUser";
    public const string TestAuthenticationScheme = "IntegrationTest";
    public const string ScopeHeaderName = "X-Test-Scp";
    public const string RoleHeaderName = "X-Test-Roles";
    public const string RequiredScope = "access_as_user";
    public const string RequiredAdmissionRole = "AssistantCore.Access";

    public static WebApplicationFactory<Program> CreateFactory(bool useTestAuthentication)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddIntegrationTestDefaults().AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret",
                            ["Microsoft365:ClientSecret"] = "integration-test-secret"
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

    public static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
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
            var claims = new List<Claim>();

            if (Request.Headers.TryGetValue(ScopeHeaderName, out var scopes))
            {
                claims.Add(new Claim("scp", scopes.ToString()));
            }

            if (Request.Headers.TryGetValue(RoleHeaderName, out var roles))
            {
                claims.Add(new Claim("roles", roles.ToString()));
            }

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, TestAuthenticationScheme));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, TestAuthenticationScheme)));
        }
    }
}
