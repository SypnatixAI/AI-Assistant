using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Conversations;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Services.Conversations;
using AssistantCore.Service.Application.Services.Messages.Authorization;
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

public sealed class ConversationMessagesEndpointTests
{
    private const string AuthenticationScheme = "TestAuthentication";

    private static readonly JsonSerializerOptions ResponseSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Given_NoAuthenticatedIdentity_When_GetMessages_Then_ReturnsUnauthorizedBeforeQuerying()
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        await using var factory = CreateFactory(listingService, useTestAuthentication: false);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync($"/api/conversations/{Guid.NewGuid()}/messages");

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AForbiddenUserContext_When_GetMessages_Then_ReturnsForbiddenBeforeQuerying()
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        await using var factory = CreateFactory(
            listingService,
            useTestAuthentication: true,
            useForbiddenUserContext: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync($"/api/conversations/{Guid.NewGuid()}/messages");

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AnEmptyConversationId_When_GetMessages_Then_ReturnsBadRequestBeforeQuerying()
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync(
            "/api/conversations/00000000-0000-0000-0000-000000000000/messages");

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    public async Task Given_AnInvalidLimit_When_GetMessages_Then_ReturnsBadRequestBeforeQuerying(
        string queryString)
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages?{queryString}");

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AnInvalidCursor_When_GetMessages_Then_ReturnsBadRequestBeforeQuerying()
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(new ConversationMessageListingPage([], null, false));
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync(
            $"/api/conversations/{Guid.NewGuid()}/messages?cursor=not-a-valid-cursor");

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AnUnknownConversation_When_GetMessages_Then_ReturnsNotFound()
    {
        // Given
        var listingService = new RecordingConversationMessageListingService(result: null);
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync($"/api/conversations/{Guid.NewGuid()}/messages");

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AValidRequest_When_GetMessages_Then_ReturnsOkWithMappedMessages()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var message = new ConversationMessageResponse(
            messageId,
            "Assistant",
            "La politique permet jusqu'a deux jours de teletravail.",
            "Completed",
            "gpt",
            now,
            now,
            [new ConversationMessageSourceResponse("SharePoint", "Politique", "https://example.com", "doc-1", now)]);
        var listingService = new RecordingConversationMessageListingService(
            new ConversationMessageListingPage([message], null, false));
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync($"/api/conversations/{conversationId}/messages?limit=10");
        var body = await response.Content.ReadFromJsonAsync<GetConversationMessagesResponse>(
            ResponseSerializerOptions);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, listingService.CallCount);
        Assert.Equal(conversationId, listingService.ReceivedConversationId);
        Assert.Equal(10, listingService.ReceivedLimit);
        Assert.NotNull(body);
        Assert.Equal(conversationId, body.ConversationId);
        var returnedMessage = Assert.Single(body.Messages);
        Assert.Equal(messageId, returnedMessage.Id);
        Assert.Equal("Assistant", returnedMessage.Role);
        var returnedSource = Assert.Single(returnedMessage.Sources);
        Assert.Equal("SharePoint", returnedSource.Type);
        Assert.False(body.HasMore);
        Assert.Null(body.NextCursor);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingConversationMessageListingService listingService,
        bool useTestAuthentication,
        bool useForbiddenUserContext = false) =>
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
                    services.RemoveAll<IConversationMessageListingService>();
                    services.AddSingleton<IConversationMessageListingService>(listingService);

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
                    services.AddSingleton<IMessageUserContextService>(
                        useForbiddenUserContext
                            ? new ForbiddenUserContextService()
                            : new StubUserContextService());
                });
            });

    private sealed class ForbiddenUserContextService : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(
            CancellationToken cancellationToken) =>
            Task.FromException<MessageUserContext>(
                new ForbiddenException("Organization access denied."));
    }

    private sealed class StubUserContextService : IMessageUserContextService
    {
        public Task<MessageUserContext> GetCurrentAsync(
            CancellationToken cancellationToken)
        {
            var organization = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "MetalPro",
                Status = RecordStatus.Active
            };
            var member = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                OrganizationId = organization.Id,
                Name = "Test User",
                Role = OrganizationRole.User,
                Status = RecordStatus.Active,
                Organization = organization
            };

            return Task.FromResult(new MessageUserContext(organization, member));
        }
    }

    private sealed class RecordingConversationMessageListingService(
        ConversationMessageListingPage? result) : IConversationMessageListingService
    {
        public int CallCount { get; private set; }

        public Guid ReceivedConversationId { get; private set; }

        public int ReceivedLimit { get; private set; }

        public Task<ConversationMessageListingPage?> ListAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            int limit,
            DateTimeOffset? cursorCreatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ReceivedConversationId = conversationId;
            ReceivedLimit = limit;
            return Task.FromResult(result);
        }
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
                [
                    new Claim(ClaimTypes.NameIdentifier, "test-user"),
                    new Claim("scp", "access_as_user"),
                    new Claim("roles", "AssistantCore.Access")
                ],
                AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
