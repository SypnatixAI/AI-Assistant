using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ConnectorSecurityTests
{
    [Theory, AutoDomainData]
    public async Task Given_GroupResolutionFails_When_SearchAsync_Then_DoesNotSearchAzure(
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        string query)
    {
        // Given
        var tenantId = Guid.NewGuid().ToString("D");
        var searchRepository = new RecordingMicrosoft365SearchRepository();
        var connector = new Microsoft365Connector(
            new FailingMicrosoft365UserGroupResolver(),
            new EmptyMicrosoft365SharePointGroupResolver(),
            searchRepository,
            new PassThroughMicrosoft365SearchAccessVerifier(),
            new Microsoft365ConnectorOptions(10, 4000),
            new EvidenceNormalizer());
        var request = new SearchMicrosoft365ToolArguments(query, null, null, null);

        // When
        var action = () => connector.SearchAsync(
            request,
            new ConnectorExecutionContext(
                organizationId,
                memberId,
                tenantId,
                entraUserId,
                IdentityProvider.MicrosoftEntraId,
                UserEmail: "user@contoso.com"),
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(0, searchRepository.SearchCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_IncoherentExecutionContext_When_SearchAsync_Then_RejectsBeforeExternalCalls(
        Guid organizationId,
        Guid memberId,
        string query)
    {
        // Given
        var groupResolver = new RecordingMicrosoft365UserGroupResolver();
        var searchRepository = new RecordingMicrosoft365SearchRepository();
        var connector = new Microsoft365Connector(
            groupResolver,
            new EmptyMicrosoft365SharePointGroupResolver(),
            searchRepository,
            new PassThroughMicrosoft365SearchAccessVerifier(),
            new Microsoft365ConnectorOptions(10, 4000),
            new EvidenceNormalizer());
        var request = new SearchMicrosoft365ToolArguments(query, null, null, null);
        var context = new ConnectorExecutionContext(
            organizationId,
            memberId,
            "tenant-id",
            Guid.NewGuid(),
            null);

        // When
        var action = () => connector.SearchAsync(
            request,
            context,
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(0, groupResolver.CallCount);
        Assert.Equal(0, searchRepository.SearchCallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_SharePointGroupResolutionFails_When_SearchAsync_Then_DoesNotSearchAzure(
        Guid organizationId,
        Guid memberId,
        Guid entraUserId,
        string query,
        string userEmail)
    {
        // Given
        var searchRepository = new RecordingMicrosoft365SearchRepository();
        var connector = new Microsoft365Connector(
            new RecordingMicrosoft365UserGroupResolver(),
            new FailingMicrosoft365SharePointGroupResolver(),
            searchRepository,
            new PassThroughMicrosoft365SearchAccessVerifier(),
            new Microsoft365ConnectorOptions(10, 4000),
            new EvidenceNormalizer());
        var request = new SearchMicrosoft365ToolArguments(query, null, null, null);

        // When
        var action = () => connector.SearchAsync(
            request,
            new ConnectorExecutionContext(
                organizationId,
                memberId,
                Guid.NewGuid().ToString("D"),
                entraUserId,
                IdentityProvider.MicrosoftEntraId,
                UserEmail: userEmail),
            CancellationToken.None);

        // Then
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Equal(0, searchRepository.SearchCallCount);
    }

    private sealed class FailingMicrosoft365UserGroupResolver : IMicrosoft365UserGroupResolver
    {
        public Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
            string externalTenantId,
            string entraUserId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Group resolution failed.");
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

    private sealed class EmptyMicrosoft365SharePointGroupResolver
        : IMicrosoft365SharePointGroupResolver
    {
        public Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
            Guid organizationId,
            string externalTenantId,
            string userEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);
    }

    private sealed class FailingMicrosoft365SharePointGroupResolver
        : IMicrosoft365SharePointGroupResolver
    {
        public Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
            Guid organizationId,
            string externalTenantId,
            string userEmail,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SharePoint group resolution failed.");
    }

    private sealed class RecordingMicrosoft365UserGroupResolver : IMicrosoft365UserGroupResolver
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
            string externalTenantId,
            string entraUserId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyCollection<string>>([]);
        }
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
            CancellationToken cancellationToken) => Task.FromResult(records);
    }
}
