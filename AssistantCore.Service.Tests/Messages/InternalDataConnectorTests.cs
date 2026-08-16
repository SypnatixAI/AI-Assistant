using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Models.Messages.Tools.Arguments;
using AssistantCore.Service.Application.Services.Messages.Connectors.InternalData;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Infrastructure.Connectors.InternalData;

namespace AssistantCore.Service.Tests.Messages;

public sealed class InternalDataConnectorTests
{
    [Theory, AutoDomainData]
    public async Task Given_ConfiguredCategories_When_SearchAsync_Then_AppliesContextLimitAndReturnsEvidence(
        Guid organizationId,
        Guid memberId,
        string query,
        Guid firstReference,
        Guid secondReference,
        DateTimeOffset occurredAt)
    {
        // Given
        var records = new[]
        {
            new InternalDataSearchRecord(
                InternalDataCategory.Messages,
                "First title",
                "First content",
                firstReference.ToString(),
                occurredAt,
                RelevanceScore: 0.9),
            new InternalDataSearchRecord(
                InternalDataCategory.Messages,
                "Second title",
                "Second content",
                secondReference.ToString(),
                occurredAt.AddMinutes(-1),
                RelevanceScore: 0.1)
        };
        var repository = new FakeInternalDataSearchRepository(records);
        var enabledCategories = new HashSet<InternalDataCategory>
        {
            InternalDataCategory.Messages
        };
        var connector = new InternalDataConnector(
            repository,
            new InternalDataConnectorOptions(
                enabledCategories,
                MaximumResults: 1,
                MaximumContentLength: 1000),
            new EvidenceNormalizer());
        var context = new ConnectorExecutionContext(organizationId, memberId);
        var request = new SearchInternalDataToolArguments(query);
        using var cancellationSource = new CancellationTokenSource();

        // When
        var result = await connector.SearchAsync(
            request,
            context,
            cancellationSource.Token);

        // Then
        Assert.Equal(organizationId, repository.ReceivedParameters?.OrganizationId);
        Assert.Equal(memberId, repository.ReceivedParameters?.MemberId);
        Assert.Equal(query, repository.ReceivedParameters?.Query);
        Assert.Equal(1, repository.ReceivedParameters?.MaximumResults);
        Assert.Equal(enabledCategories, repository.ReceivedParameters?.Categories);
        Assert.Equal(cancellationSource.Token, repository.ReceivedCancellationToken);

        var evidence = Assert.Single(result.Evidence);
        Assert.Equal("InternalMessage", evidence.SourceType);
        Assert.Equal($"InternalMessage:{firstReference}", evidence.Reference);
        Assert.Null(evidence.Url);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACancelledRequest_When_SearchAsync_Then_PropagatesCancellation(
        Guid organizationId,
        Guid memberId,
        string query)
    {
        // Given
        var repository = new FakeInternalDataSearchRepository([]);
        var connector = new InternalDataConnector(
            repository,
            new InternalDataConnectorOptions(
                new HashSet<InternalDataCategory> { InternalDataCategory.Messages },
                MaximumResults: 10,
                MaximumContentLength: 1000),
            new EvidenceNormalizer());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        // When
        var exception = await Record.ExceptionAsync(() => connector.SearchAsync(
            new SearchInternalDataToolArguments(query),
            new ConnectorExecutionContext(organizationId, memberId),
            cancellationSource.Token));

        // Then
        Assert.IsType<OperationCanceledException>(exception);
    }

    private sealed class FakeInternalDataSearchRepository(
        IReadOnlyCollection<InternalDataSearchRecord> records)
        : IInternalDataSearchRepository
    {
        public InternalDataSearchParameters? ReceivedParameters { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IReadOnlyCollection<InternalDataSearchRecord>> SearchAsync(
            InternalDataSearchParameters parameters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedParameters = parameters;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(records);
        }
    }
}
