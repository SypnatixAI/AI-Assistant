using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using AssistantCore.Repository.Abstractions;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Messages.Authorization;
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

public sealed class MessagesEndpointAuthorizationTests
{
    private const string AuthenticationScheme = "TestAuthentication";

    [Theory, AutoDomainData]
    public async Task Given_NoAuthenticatedIdentity_When_PostMessages_Then_ReturnsUnauthorizedBeforeSending(
        Guid conversationId)
    {
        // Given
        var lifecycleService = new RecordingMessageProcessingLifecycleService();
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: false);
        using var client = factory.CreateClient();
        using var content = CreateContent(conversationId);

        // When
        using var response = await client.PostAsync("/api/messages", content);

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, lifecycleService.StartCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_AForbiddenUserContext_When_PostMessages_Then_ReturnsForbiddenBeforeSending(
        Guid conversationId)
    {
        // Given
        var lifecycleService = new RecordingMessageProcessingLifecycleService();
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();
        using var content = CreateContent(conversationId);

        // When
        using var response = await client.PostAsync("/api/messages", content);

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, lifecycleService.StartCallCount);
    }

    private static StringContent CreateContent(Guid conversationId)
    {
        var content = new StringContent(
            $$"""
              {"conversationId":"{{conversationId}}","message":"Question"}
              """,
            Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingMessageProcessingLifecycleService lifecycleService,
        bool useTestAuthentication) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Development);
                builder.ConfigureAppConfiguration(configuration =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret",
                            ["Microsoft365:ClientSecret"] = "integration-test-secret"
                        }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IMessageProcessingLifecycleService>();
                    services.AddSingleton<IMessageProcessingLifecycleService>(lifecycleService);

                    if (!useTestAuthentication)
                    {
                        return;
                    }

                    services
                        .AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = AuthenticationScheme;
                            options.DefaultChallengeScheme = AuthenticationScheme;
                        })
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            AuthenticationScheme,
                            _ => { });
                    services.RemoveAll<IMessageUserContextService>();
                    services.AddSingleton<IMessageUserContextService, ForbiddenUserContextService>();
                });
            });

    private sealed class ForbiddenUserContextService : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(
            CancellationToken cancellationToken) =>
            Task.FromException<MessageUserContext>(
                new ForbiddenException("Organization access denied."));
    }

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
