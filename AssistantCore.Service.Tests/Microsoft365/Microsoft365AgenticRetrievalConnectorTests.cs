using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;
using AssistantCore.Service.Application.Models.Messages.AiModels;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Rag;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365AgenticRetrievalConnectorTests
{
    [Theory, InlineAutoDomainData("code projet Atlas")]
    public async Task Given_AgenticRetrievalEnabled_When_SearchAsync_Then_RetrievesWithConversationAndAclFilter(
        string query,
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        Guid entraGroupId,
        string title,
        string content,
        string userEmail)
    {
        // Given
        var sharePointGroupId = "spg:contoso.sharepoint.com,site-collection-id,web-id:5";
        var retrievalClient = new RecordingAgenticRetrievalClient(
            new AgenticRetrievalReference(
                "0",
                "chunk-atlas",
                title,
                content,
                "site-id",
                "drive-id",
                "drive-item-id",
                "https://contoso.example/atlas",
                new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
                3.1d));
        var connector = CreateConnector(
            organizationId,
            entraGroupId,
            sharePointGroupId,
            retrievalClient);
        var context = CreateContext(
            organizationId,
            memberId,
            entraUserId,
            userEmail) with
        {
            ConversationHistory =
            [
                new AiConversationMessage(AiConversationRole.User, "parle-moi du projet Atlas"),
                new AiConversationMessage(AiConversationRole.Assistant, "Atlas est un projet suivi.")
            ]
        };

        // When
        var result = await connector.SearchAsync(
            new SearchMicrosoft365ToolArguments(
                query,
                ["sharepoint"],
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31)),
            context,
            CancellationToken.None);

        // Then
        var request = Assert.Single(retrievalClient.ReceivedRequests);
        Assert.Equal("SynaptixKnowledgeBase", request.KnowledgeBaseName);
        Assert.Equal("Microsoft365KnowledgeSource", request.KnowledgeSourceName);
        Assert.Equal(query, request.Query);
        Assert.Equal(2, request.ConversationHistory.Count);
        Assert.Contains($"organizationId eq '{organizationId:D}'", request.Filter, StringComparison.Ordinal);
        Assert.Contains($"allowedUserIds/any(id: id eq '{entraUserId:D}')", request.Filter, StringComparison.Ordinal);
        Assert.Contains(entraGroupId.ToString("D"), request.Filter, StringComparison.Ordinal);
        Assert.Contains(sharePointGroupId, request.Filter, StringComparison.Ordinal);
        Assert.Contains("sourceType eq 'sharepoint'", request.Filter, StringComparison.Ordinal);
        Assert.Contains("modifiedAt ge 2026-01-01T00:00:00Z", request.Filter, StringComparison.Ordinal);
        Assert.Contains("modifiedAt lt 2026-02-01T00:00:00Z", request.Filter, StringComparison.Ordinal);
        var evidence = Assert.Single(result.Evidence);
        Assert.Equal(title, evidence.Title);
        Assert.Equal(content, evidence.Content);
    }

    [Theory, InlineAutoDomainData("compare les risques financiers d'Atlas et MécanoPlus")]
    public async Task Given_AgenticRetrievalEnabled_When_SearchAsync_Then_BypassesCustomMultiQueryAndCorrectiveRetrieval(
        string query,
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        Guid entraGroupId,
        string userEmail)
    {
        // Given
        var searchRepository = new RecordingMicrosoft365SearchRepository();
        var retrievalClient = new RecordingAgenticRetrievalClient(
            CreateReference("0", "chunk-atlas", "Atlas", "Atlas risk content."));
        var connector = new Microsoft365Connector(
            new StaticMicrosoft365UserGroupResolver([entraGroupId.ToString("D")]),
            new StaticMicrosoft365SharePointGroupResolver([]),
            searchRepository,
            new PassThroughMicrosoft365SearchAccessVerifier(),
            CreateOptions(),
            new EvidenceNormalizer(),
            new FailingMicrosoft365QueryExpansionService(),
            new Microsoft365SearchResultFusionService(),
            correctiveRetrieval: new FailingCorrectiveRetrievalService(),
            agenticRetrievalClient: retrievalClient,
            searchOptions: CreateSearchOptions());

        // When
        var result = await connector.SearchAsync(
            new SearchMicrosoft365ToolArguments(query, null, null, null),
            CreateContext(organizationId, memberId, entraUserId, userEmail),
            CancellationToken.None);

        // Then
        Assert.Equal(0, searchRepository.SearchCallCount);
        Assert.Single(retrievalClient.ReceivedRequests);
        Assert.Single(result.Evidence);
    }

    [Theory, InlineAutoDomainData("document inaccessible Atlas")]
    public async Task Given_AgenticRetrievalReturnsUnauthorizedDocument_When_SearchAsync_Then_EvidenceIsNotReturned(
        string query,
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        Guid entraGroupId,
        string userEmail)
    {
        // Given
        var connector = new Microsoft365Connector(
            new StaticMicrosoft365UserGroupResolver([entraGroupId.ToString("D")]),
            new StaticMicrosoft365SharePointGroupResolver([]),
            new RecordingMicrosoft365SearchRepository(),
            new RejectingMicrosoft365SearchAccessVerifier(),
            CreateOptions(),
            new EvidenceNormalizer(),
            agenticRetrievalClient: new RecordingAgenticRetrievalClient(
                CreateReference("0", "chunk-secret", "Secret", "Restricted content.")),
            searchOptions: CreateSearchOptions());

        // When
        var result = await connector.SearchAsync(
            new SearchMicrosoft365ToolArguments(query, null, null, null),
            CreateContext(organizationId, memberId, entraUserId, userEmail),
            CancellationToken.None);

        // Then
        Assert.Empty(result.Evidence);
    }

    private static Microsoft365Connector CreateConnector(
        Guid organizationId,
        Guid entraGroupId,
        string sharePointGroupId,
        IAgenticRetrievalClient retrievalClient) =>
        new(
            new StaticMicrosoft365UserGroupResolver([entraGroupId.ToString("D")]),
            new StaticMicrosoft365SharePointGroupResolver([sharePointGroupId]),
            new RecordingMicrosoft365SearchRepository(),
            new PassThroughMicrosoft365SearchAccessVerifier(),
            CreateOptions(),
            new EvidenceNormalizer(),
            agenticRetrievalClient: retrievalClient,
            searchOptions: CreateSearchOptions());

    private static ConnectorExecutionContext CreateContext(
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        string userEmail) =>
        new(
            organizationId,
            memberId,
            Guid.NewGuid().ToString("D"),
            entraUserId,
            IdentityProvider.MicrosoftEntraId,
            UserEmail: userEmail);

    private static Microsoft365ConnectorOptions CreateOptions() =>
        new(
            10,
            4000,
            Microsoft365QueryExpansionOptions.Disabled,
            new Microsoft365AgenticRetrievalOptions(true, 30, 6000));

    private static IOptions<AzureAiSearchOptions> CreateSearchOptions() =>
        Options.Create(new AzureAiSearchOptions
        {
            Endpoint = "https://search.example",
            IndexName = "content-index",
            KnowledgeBaseName = "SynaptixKnowledgeBase",
            KnowledgeSourceName = "Microsoft365KnowledgeSource"
        });

    private static AgenticRetrievalReference CreateReference(
        string referenceId,
        string documentKey,
        string title,
        string content) =>
        new(
            referenceId,
            documentKey,
            title,
            content,
            "site-id",
            "drive-id",
            "drive-item-id",
            "https://contoso.example/document",
            DateTimeOffset.UtcNow,
            3.0d);

    private sealed class RecordingAgenticRetrievalClient(
        params AgenticRetrievalReference[] references) : IAgenticRetrievalClient
    {
        public List<AgenticRetrievalRequest> ReceivedRequests { get; } = [];

        public Task<AgenticRetrievalResult> RetrieveAsync(
            AgenticRetrievalRequest request,
            CancellationToken cancellationToken)
        {
            ReceivedRequests.Add(request);
            return Task.FromResult(new AgenticRetrievalResult(
                "merged content",
                references,
                [
                    new AgenticRetrievalActivity(
                        "searchIndex",
                        request.KnowledgeSourceName,
                        "project Atlas financial risks",
                        1,
                        100),
                    new AgenticRetrievalActivity(
                        "searchIndex",
                        request.KnowledgeSourceName,
                        "MecanoPlus financial risks",
                        1,
                        100)
                ]));
        }
    }

    private sealed class RecordingMicrosoft365SearchRepository : IMicrosoft365SearchRepository
    {
        public int SearchCallCount { get; private set; }

        public Task<IReadOnlyCollection<Microsoft365SearchRecord>> SearchAsync(
            Microsoft365SearchParameters parameters,
            CancellationToken cancellationToken)
        {
            SearchCallCount++;
            return Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>([]);
        }
    }

    private sealed class FailingMicrosoft365QueryExpansionService : IMicrosoft365QueryExpansionService
    {
        public Task<IReadOnlyCollection<string>> ExpandAsync(
            string query,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Query expansion should be bypassed.");
    }

    private sealed class FailingCorrectiveRetrievalService : ICorrectiveRetrievalService
    {
        public Task<IReadOnlyCollection<Microsoft365SearchRecord>> RetrieveAsync(
            Microsoft365SearchParameters parameters,
            ConnectorExecutionContext context,
            Func<Microsoft365SearchParameters, CancellationToken, Task<IReadOnlyCollection<Microsoft365SearchRecord>>> authorizedSearch,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Corrective retrieval should be bypassed.");
    }

    private sealed class StaticMicrosoft365UserGroupResolver(
        IReadOnlyCollection<string> groupIds) : IMicrosoft365UserGroupResolver
    {
        public Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
            string externalTenantId,
            string entraUserId,
            CancellationToken cancellationToken) =>
            Task.FromResult(groupIds);
    }

    private sealed class StaticMicrosoft365SharePointGroupResolver(
        IReadOnlyCollection<string> groupIds) : IMicrosoft365SharePointGroupResolver
    {
        public Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
            Guid organizationId,
            string externalTenantId,
            string userEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult(groupIds);
    }

    private sealed class PassThroughMicrosoft365SearchAccessVerifier
        : IMicrosoft365SearchAccessVerifier
    {
        public Task<IReadOnlyCollection<Microsoft365SearchRecord>> KeepAuthorizedAsync(
            Guid organizationId,
            string externalTenantId,
            string entraUserId,
            IReadOnlyCollection<string> entraGroupIds,
            IReadOnlyCollection<string> sharePointGroupIds,
            IReadOnlyCollection<Microsoft365SearchRecord> records,
            CancellationToken cancellationToken) =>
            Task.FromResult(records);
    }

    private sealed class RejectingMicrosoft365SearchAccessVerifier
        : IMicrosoft365SearchAccessVerifier
    {
        public Task<IReadOnlyCollection<Microsoft365SearchRecord>> KeepAuthorizedAsync(
            Guid organizationId,
            string externalTenantId,
            string entraUserId,
            IReadOnlyCollection<string> entraGroupIds,
            IReadOnlyCollection<string> sharePointGroupIds,
            IReadOnlyCollection<Microsoft365SearchRecord> records,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>([]);
    }
}
