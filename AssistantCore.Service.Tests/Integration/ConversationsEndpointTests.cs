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

public sealed class ConversationsEndpointTests
{
    private const string AuthenticationScheme = "TestAuthentication";

    private static readonly JsonSerializerOptions ResponseSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Given_NoAuthenticatedIdentity_When_GetConversations_Then_ReturnsUnauthorizedBeforeQuerying()
    {
        // Given
        var listingService = new RecordingConversationListingService();
        await using var factory = CreateFactory(listingService, useTestAuthentication: false);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync("/api/conversations");

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AForbiddenUserContext_When_GetConversations_Then_ReturnsForbiddenBeforeQuerying()
    {
        // Given
        var listingService = new RecordingConversationListingService();
        await using var factory = CreateFactory(
            listingService,
            useTestAuthentication: true,
            useForbiddenUserContext: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync("/api/conversations");

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    public async Task Given_AnInvalidLimit_When_GetConversations_Then_ReturnsBadRequestBeforeQuerying(
        string queryString)
    {
        // Given
        var listingService = new RecordingConversationListingService();
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync($"/api/conversations?{queryString}");

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AnInvalidCursor_When_GetConversations_Then_ReturnsBadRequestBeforeQuerying()
    {
        // Given
        var listingService = new RecordingConversationListingService();
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync("/api/conversations?cursor=not-a-valid-cursor");

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    [Fact]
    public async Task Given_AValidRequest_When_GetConversations_Then_ReturnsOkWithMappedConversations()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow;
        var summary = new ConversationSummaryResponse(
            conversationId,
            "Politique de teletravail",
            nameof(ConversationStatus.Active),
            7,
            updatedAt.AddMinutes(-5),
            updatedAt,
            "La politique permet jusqu'a deux jours...");
        var listingService = new RecordingConversationListingService(
            new ConversationListingPage([summary], HasMore: false));
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync("/api/conversations?limit=10");
        var body = await response.Content.ReadFromJsonAsync<ListConversationsResponse>(
            ResponseSerializerOptions);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, listingService.CallCount);
        Assert.Equal(10, listingService.ReceivedLimit);
        Assert.NotNull(body);
        var returnedConversation = Assert.Single(body.Conversations);
        Assert.Equal(conversationId, returnedConversation.Id);
        Assert.Equal("Politique de teletravail", returnedConversation.Title);
        Assert.Equal("La politique permet jusqu'a deux jours...", returnedConversation.LastMessagePreview);
        Assert.Equal(nameof(ConversationStatus.Active), returnedConversation.Status);
        Assert.Equal(7, returnedConversation.Version);
        Assert.False(body.HasMore);
        Assert.Null(body.NextCursor);
    }

    [Fact]
    public async Task Given_TheArchivedStatus_When_GetConversations_Then_ForwardsItToTheListingService()
    {
        // Given
        var listingService = new RecordingConversationListingService();
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync("/api/conversations?status=Archived");

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, listingService.CallCount);
        Assert.Equal(ConversationStatus.Archived, listingService.ReceivedStatus);
    }

    [Fact]
    public async Task Given_NoStatus_When_GetConversations_Then_ListsActiveConversations()
    {
        // Given
        var listingService = new RecordingConversationListingService();
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync("/api/conversations");

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ConversationStatus.Active, listingService.ReceivedStatus);
    }

    [Theory]
    [InlineData("status=Deleted")]
    [InlineData("status=archived")]
    public async Task Given_AnInvalidStatus_When_GetConversations_Then_ReturnsBadRequestBeforeQuerying(
        string queryString)
    {
        // Given
        var listingService = new RecordingConversationListingService();
        await using var factory = CreateFactory(listingService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.GetAsync($"/api/conversations?{queryString}");

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, listingService.CallCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingConversationListingService listingService,
        bool useTestAuthentication,
        bool useForbiddenUserContext = false) =>
        new WebApplicationFactory<Program>()
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
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IConversationListingService>();
                    services.AddSingleton<IConversationListingService>(listingService);

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

    private sealed class RecordingConversationListingService(
        ConversationListingPage? page = null) : IConversationListingService
    {
        public int CallCount { get; private set; }

        public int ReceivedLimit { get; private set; }

        public ConversationStatus ReceivedStatus { get; private set; }

        public Task<ConversationListingPage> ListAsync(
            Guid organizationId,
            Guid ownerMemberId,
            ConversationStatus status,
            int limit,
            DateTimeOffset? cursorUpdatedAt,
            Guid? cursorId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            ReceivedLimit = limit;
            ReceivedStatus = status;
            return Task.FromResult(page ?? new ConversationListingPage([], HasMore: false));
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
