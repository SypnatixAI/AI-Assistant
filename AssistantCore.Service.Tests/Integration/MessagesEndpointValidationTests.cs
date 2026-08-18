using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using AssistantCore.Service.Application.Services.Messages.Lifecycle;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Integration;

public sealed class MessagesEndpointValidationTests
{
    private const string AuthenticationScheme = "TestAuthentication";

    [Theory]
    [InlineAutoDomainData("")]
    [InlineAutoDomainData("{\"conversationId\":\"not-a-guid\",\"message\":\"Question\"}")]
    public async Task Given_AnInvalidHttpBody_When_PostMessages_Then_ReturnsBadRequestBeforeSending(
        string body)
    {
        // Given
        var lifecycleService = new RecordingMessageProcessingLifecycleService();
        await using var factory = CreateFactory(lifecycleService);
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        // When
        using var response = await client.PostAsync("/api/messages", content);

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, lifecycleService.StartCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_InvalidMessageValues_When_PostMessages_Then_ReturnsBadRequestBeforeSending(
        Guid conversationId)
    {
        // Given
        var lifecycleService = new RecordingMessageProcessingLifecycleService();
        await using var factory = CreateFactory(lifecycleService);
        using var client = factory.CreateClient();
        var invalidBodies = new[]
        {
            $$"""
              {"conversationId":"{{conversationId}}","message":"{{new string('a', 4001)}}"}
              """,
            $$"""
              {"conversationId":"{{conversationId}}","message":"Question","model":"gpt-unavailable"}
              """
        };

        foreach (var body in invalidBodies)
        {
            using var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // When
            using var response = await client.PostAsync("/api/messages", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            // Then
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 400 Bad Request but received {(int)response.StatusCode}: {responseBody}");
        }

        Assert.Equal(0, lifecycleService.StartCallCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingMessageProcessingLifecycleService lifecycleService) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret"
                        }));
                builder.ConfigureTestServices(services =>
                {
                    services
                        .AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = AuthenticationScheme;
                            options.DefaultChallengeScheme = AuthenticationScheme;
                        })
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            AuthenticationScheme,
                            _ => { });
                    services.RemoveAll<IMessageProcessingLifecycleService>();
                    services.AddSingleton<IMessageProcessingLifecycleService>(lifecycleService);
                });
            });

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user")],
                AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
