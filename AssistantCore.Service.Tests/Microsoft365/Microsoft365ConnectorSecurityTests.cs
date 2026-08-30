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
            searchRepository,
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
                IdentityProvider.MicrosoftEntraId),
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
            searchRepository,
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
}
