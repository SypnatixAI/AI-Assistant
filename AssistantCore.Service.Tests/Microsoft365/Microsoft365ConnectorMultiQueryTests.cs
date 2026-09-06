using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ConnectorMultiQueryTests
{
    [Theory, AutoDomainData]
    public async Task Given_QueryExpansionReturnsVariants_When_SearchAsync_Then_SearchesEachQueryWithSameAcl(
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        Guid entraGroupId,
        string originalQuery,
        string expandedQuery,
        string userEmail)
    {
        // Given
        var tenantId = Guid.NewGuid().ToString("D");
        var sharePointGroupId = "spg:contoso.sharepoint.com,site-collection-id,web-id:5";
        var searchRepository = new RecordingMicrosoft365SearchRepository();
        var accessVerifier = new RecordingMicrosoft365SearchAccessVerifier();
        var connector = new Microsoft365Connector(
            new StaticMicrosoft365UserGroupResolver([entraGroupId.ToString("D")]),
            new StaticMicrosoft365SharePointGroupResolver([sharePointGroupId]),
            searchRepository,
            accessVerifier,
            new Microsoft365ConnectorOptions(10, 4000),
            new EvidenceNormalizer(),
            new StaticMicrosoft365QueryExpansionService([originalQuery, expandedQuery]),
            new Microsoft365SearchResultFusionService());
        var request = new SearchMicrosoft365ToolArguments(
            originalQuery,
            ["sharepoint"],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));
        var context = new ConnectorExecutionContext(
            organizationId,
            memberId,
            tenantId,
            entraUserId,
            IdentityProvider.MicrosoftEntraId,
            UserEmail: userEmail);

        // When
        await connector.SearchAsync(request, context, CancellationToken.None);

        // Then
        Assert.Equal(
            new[] { originalQuery, expandedQuery },
            searchRepository.ReceivedParameters.Select(item => item.Query));
        Assert.All(searchRepository.ReceivedParameters, parameters =>
        {
            Assert.Equal(organizationId, parameters.SecurityContext.OrganizationId);
            Assert.Equal(entraUserId.ToString("D"), parameters.SecurityContext.EntraUserId);
            Assert.Equal(
                new[] { entraGroupId.ToString("D") },
                parameters.SecurityContext.EntraGroupIds);
            Assert.Equal(new[] { sharePointGroupId }, parameters.SecurityContext.SharePointGroupIds);
            Assert.Equal(request.SourceTypes, parameters.SourceTypes);
            Assert.Equal(request.DateFrom, parameters.DateFrom);
            Assert.Equal(request.DateTo, parameters.DateTo);
        });
        Assert.Equal(2, accessVerifier.ReceivedRecords.Count);
    }

    [Theory, AutoDomainData]
    public async Task Given_QueryExpansionReturnsDuplicateResults_When_SearchAsync_Then_AccessVerifierReceivesDeduplicatedRecords(
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        string originalQuery,
        string expandedQuery,
        string duplicateChunkId,
        string userEmail)
    {
        // Given
        var searchRepository = new RecordingMicrosoft365SearchRepository(duplicateChunkId);
        var accessVerifier = new RecordingMicrosoft365SearchAccessVerifier();
        var connector = new Microsoft365Connector(
            new StaticMicrosoft365UserGroupResolver([]),
            new StaticMicrosoft365SharePointGroupResolver([]),
            searchRepository,
            accessVerifier,
            new Microsoft365ConnectorOptions(10, 4000),
            new EvidenceNormalizer(),
            new StaticMicrosoft365QueryExpansionService([originalQuery, expandedQuery]),
            new Microsoft365SearchResultFusionService());
        var context = new ConnectorExecutionContext(
            organizationId,
            memberId,
            Guid.NewGuid().ToString("D"),
            entraUserId,
            IdentityProvider.MicrosoftEntraId,
            UserEmail: userEmail);

        // When
        await connector.SearchAsync(
            new SearchMicrosoft365ToolArguments(originalQuery, null, null, null),
            context,
            CancellationToken.None);

        // Then
        var record = Assert.Single(accessVerifier.ReceivedRecords);
        Assert.Equal(duplicateChunkId, record.Reference);
    }

    private sealed class StaticMicrosoft365QueryExpansionService(
        IReadOnlyCollection<string> queries) : IMicrosoft365QueryExpansionService
    {
        public Task<IReadOnlyCollection<string>> ExpandAsync(
            string query,
            CancellationToken cancellationToken) =>
            Task.FromResult(queries);
    }

    private sealed class RecordingMicrosoft365SearchRepository(
        string? fixedChunkId = null) : IMicrosoft365SearchRepository
    {
        public List<Microsoft365SearchParameters> ReceivedParameters { get; } = [];

        public Task<IReadOnlyCollection<Microsoft365SearchRecord>> SearchAsync(
            Microsoft365SearchParameters parameters,
            CancellationToken cancellationToken)
        {
            ReceivedParameters.Add(parameters);
            var chunkId = fixedChunkId ?? $"chunk-{ReceivedParameters.Count}";

            return Task.FromResult<IReadOnlyCollection<Microsoft365SearchRecord>>(
                [
                    new Microsoft365SearchRecord(
                        "Microsoft365",
                        $"Title {ReceivedParameters.Count}",
                        "Content",
                        chunkId,
                        "site-id",
                        "drive-id",
                        "drive-item-id",
                        "https://contoso.example/document",
                        DateTimeOffset.UtcNow,
                        1d)
                ]);
        }
    }

    private sealed class RecordingMicrosoft365SearchAccessVerifier
        : IMicrosoft365SearchAccessVerifier
    {
        public IReadOnlyCollection<Microsoft365SearchRecord> ReceivedRecords { get; private set; } = [];

        public Task<IReadOnlyCollection<Microsoft365SearchRecord>> KeepAuthorizedAsync(
            Guid organizationId,
            string externalTenantId,
            string entraUserId,
            IReadOnlyCollection<string> entraGroupIds,
            IReadOnlyCollection<string> sharePointGroupIds,
            IReadOnlyCollection<Microsoft365SearchRecord> records,
            CancellationToken cancellationToken)
        {
            ReceivedRecords = records;
            return Task.FromResult(records);
        }
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
}
