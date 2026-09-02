using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Exceptions;
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

public sealed class ConversationLifecycleEndpointTests
{
    private const string AuthenticationScheme = "TestAuthentication";

    private static readonly JsonSerializerOptions ResponseSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Given_NoAuthenticatedIdentity_When_PatchConversation_Then_ReturnsUnauthorizedBeforeUpdating()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService();
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: false);
        using var client = factory.CreateClient();
        using var content = CreatePatchContent("Nouveau titre");

        // When
        using var response = await client.PatchAsync(
            $"/api/conversations/{Guid.NewGuid()}",
            content);

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, lifecycleService.UpdateCallCount);
    }

    [Fact]
    public async Task Given_AForbiddenUserContext_When_PatchConversation_Then_ReturnsForbiddenBeforeUpdating()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService();
        await using var factory = CreateFactory(
            lifecycleService,
            useTestAuthentication: true,
            useForbiddenUserContext: true);
        using var client = factory.CreateClient();
        using var content = CreatePatchContent("Nouveau titre");

        // When
        using var response = await client.PatchAsync(
            $"/api/conversations/{Guid.NewGuid()}",
            content);

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, lifecycleService.UpdateCallCount);
    }

    [Fact]
    public async Task Given_AnIfMatchHeader_When_PatchConversation_Then_ReturnsOkAndForwardsTheExpectedVersion()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow;
        var lifecycleService = new RecordingConversationLifecycleService
        {
            UpdateResult = new ConversationResponse(
                conversationId,
                "Politique de teletravail",
                nameof(ConversationStatus.Archived),
                updatedAt,
                8)
        };
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/conversations/{conversationId}")
        {
            Content = CreatePatchContent("Politique de teletravail", "Archived")
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"7\"");

        // When
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ConversationResponse>(
            ResponseSerializerOptions);

        // Then
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(conversationId, body.Id);
        Assert.Equal(8, body.Version);
        Assert.Equal(nameof(ConversationStatus.Archived), body.Status);
        Assert.Equal(conversationId, lifecycleService.ReceivedConversationId);
        Assert.Equal(7, lifecycleService.ReceivedExpectedVersion);
    }

    [Fact]
    public async Task Given_AConversationOfAnotherMember_When_PatchConversation_Then_ReturnsNotFound()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService
        {
            UpdateException = new NotFoundException("Conversation not found.")
        };
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();
        using var content = CreatePatchContent("Titre vole");

        // When
        using var response = await client.PatchAsync(
            $"/api/conversations/{Guid.NewGuid()}",
            content);

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Given_AConcurrentModification_When_PatchConversation_Then_ReturnsConflictWithTheStableCode()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService
        {
            UpdateException = new ConflictException(
                "The conversation was modified in another session.",
                ConflictException.ConversationVersionConflict)
        };
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();
        using var content = CreatePatchContent("Titre concurrent");

        // When
        using var response = await client.PatchAsync(
            $"/api/conversations/{Guid.NewGuid()}",
            content);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(ResponseSerializerOptions);

        // Then
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(ConflictException.ConversationVersionConflict, body.Code);
    }

    [Fact]
    public async Task Given_AnEmptyPatch_When_PatchConversation_Then_ReturnsBadRequest()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService
        {
            UpdateException = new BadRequestException(
                "At least one of 'title' or 'status' must be provided.")
        };
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        // When
        using var response = await client.PatchAsync(
            $"/api/conversations/{Guid.NewGuid()}",
            content);

        // Then
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Given_NoAuthenticatedIdentity_When_DeleteConversation_Then_ReturnsUnauthorizedBeforeDeleting()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService();
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: false);
        using var client = factory.CreateClient();

        // When
        using var response = await client.DeleteAsync($"/api/conversations/{Guid.NewGuid()}");

        // Then
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, lifecycleService.DeleteCallCount);
    }

    [Fact]
    public async Task Given_AForbiddenUserContext_When_DeleteConversation_Then_ReturnsForbiddenBeforeDeleting()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService();
        await using var factory = CreateFactory(
            lifecycleService,
            useTestAuthentication: true,
            useForbiddenUserContext: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.DeleteAsync($"/api/conversations/{Guid.NewGuid()}");

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, lifecycleService.DeleteCallCount);
    }

    [Fact]
    public async Task Given_AVisibleConversation_When_DeleteConversation_Then_ReturnsNoContent()
    {
        // Given
        var conversationId = Guid.NewGuid();
        var lifecycleService = new RecordingConversationLifecycleService();
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.DeleteAsync($"/api/conversations/{conversationId}");

        // Then
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(conversationId, lifecycleService.ReceivedConversationId);
    }

    [Fact]
    public async Task Given_AnAlreadyDeletedConversation_When_DeleteConversation_Then_StillReturnsNoContent()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService
        {
            DeleteResult = true
        };
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();
        var conversationId = Guid.NewGuid();

        // When
        using var first = await client.DeleteAsync($"/api/conversations/{conversationId}");
        using var second = await client.DeleteAsync($"/api/conversations/{conversationId}");

        // Then
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Given_AConversationOfAnotherMember_When_DeleteConversation_Then_ReturnsNotFound()
    {
        // Given
        var lifecycleService = new RecordingConversationLifecycleService
        {
            DeleteException = new NotFoundException("Conversation not found.")
        };
        await using var factory = CreateFactory(lifecycleService, useTestAuthentication: true);
        using var client = factory.CreateClient();

        // When
        using var response = await client.DeleteAsync($"/api/conversations/{Guid.NewGuid()}");

        // Then
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static StringContent CreatePatchContent(string title, string? status = null)
    {
        var payload = status is null
            ? $$"""{"title":"{{title}}"}"""
            : $$"""{"title":"{{title}}","status":"{{status}}"}""";

        return new StringContent(payload, Encoding.UTF8, "application/json");
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingConversationLifecycleService lifecycleService,
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
                    services.RemoveAll<IConversationLifecycleService>();
                    services.AddSingleton<IConversationLifecycleService>(lifecycleService);

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

    private sealed record ErrorBody(string Message, string? Detail, string? Code);

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

    private sealed class RecordingConversationLifecycleService : IConversationLifecycleService
    {
        public ConversationResponse? UpdateResult { get; init; }

        public Exception? UpdateException { get; init; }

        public bool DeleteResult { get; init; }

        public Exception? DeleteException { get; init; }

        public int UpdateCallCount { get; private set; }

        public int DeleteCallCount { get; private set; }

        public Guid ReceivedConversationId { get; private set; }

        public int? ReceivedExpectedVersion { get; private set; }

        public Task<ConversationResponse> UpdateAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            string? title,
            string? status,
            int? expectedVersion,
            CancellationToken cancellationToken)
        {
            UpdateCallCount++;
            ReceivedConversationId = conversationId;
            ReceivedExpectedVersion = expectedVersion;

            if (UpdateException is not null)
            {
                return Task.FromException<ConversationResponse>(UpdateException);
            }

            return Task.FromResult(
                UpdateResult
                ?? new ConversationResponse(
                    conversationId,
                    title ?? "Titre",
                    status ?? nameof(ConversationStatus.Active),
                    DateTimeOffset.UtcNow,
                    1));
        }

        public Task<bool> DeleteAsync(
            Guid organizationId,
            Guid ownerMemberId,
            Guid conversationId,
            CancellationToken cancellationToken)
        {
            DeleteCallCount++;
            ReceivedConversationId = conversationId;

            return DeleteException is not null
                ? Task.FromException<bool>(DeleteException)
                : Task.FromResult(DeleteResult);
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
