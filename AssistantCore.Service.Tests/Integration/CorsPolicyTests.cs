using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AssistantCore.Service.Tests.Integration;

public sealed class CorsPolicyTests
{
    private const string AllowedOrigin = "http://localhost:4200";
    private const string LocalEnvironmentName = "Local";

    [Theory, AutoDomainData]
    public async Task Given_AllowedOrigin_When_OptionsRequestIsSent_Then_CorsOriginIsReturned(
        bool _)
    {
        // Given
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var request = CreatePreflightRequest(AllowedOrigin);

        // When
        using var response = await client.SendAsync(request);

        // Then
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            AllowedOrigin,
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Theory, AutoDomainData]
    public async Task Given_UnknownOrigin_When_OptionsRequestIsSent_Then_CorsOriginIsNotReturned(
        bool _)
    {
        // Given
        await using var factory = CreateFactory();
        using var client = CreateClient(factory);
        using var request = CreatePreflightRequest("https://unknown.example.test");

        // When
        using var response = await client.SendAsync(request);

        // Then
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(LocalEnvironmentName);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddIntegrationTestDefaults().AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret",
                            ["Microsoft365:ClientSecret"] = "integration-test-secret"
                        }));
            });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/core/authenticateUser");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }
}
