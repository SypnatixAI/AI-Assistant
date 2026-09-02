using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AssistantCore.Service.Tests.Integration;

public sealed class MicrosoftGraphWebhookEndpointTests
{
    [Theory, AutoDomainData]
    public async Task Given_APlainTextValidationRequest_When_PostWebhook_Then_ValidationTokenIsReturnedExactly(
        string validationToken)
    {
        // Given
        await using var factory = new WebApplicationFactory<Program>()
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
            });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/webhooks/microsoft-graph?validationToken={Uri.EscapeDataString(validationToken)}")
        {
            Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "text/plain")
        };

        // When
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(validationToken, responseBody);
    }
}
