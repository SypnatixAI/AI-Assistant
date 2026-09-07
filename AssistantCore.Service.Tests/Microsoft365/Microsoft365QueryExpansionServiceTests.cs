using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365QueryExpansionServiceTests
{
    [Theory, AutoDomainData]
    public async Task Given_QueryExpansionDisabled_When_ExpandAsync_Then_ReturnsOnlyOriginalQuery(
        string query)
    {
        // Given
        var client = new RecordingQueryExpansionClient([]);
        var service = CreateService(
            client,
            new AlwaysExpandPolicy(),
            Microsoft365QueryExpansionOptions.Disabled);

        // When
        var queries = await service.ExpandAsync($"  {query}  ", CancellationToken.None);

        // Then
        Assert.Equal(new[] { query }, queries);
        Assert.Equal(0, client.CallCount);
    }

    [Theory, AutoDomainData]
    public async Task Given_DuplicateAndEmptyGeneratedQueries_When_ExpandAsync_Then_NormalizesAndKeepsOriginalQuery(
        string originalQuery,
        string firstVariant,
        string secondVariant)
    {
        // Given
        var client = new RecordingQueryExpansionClient(
            [
                "",
                firstVariant,
                originalQuery,
                $" {firstVariant} ",
                secondVariant
            ]);
        var service = CreateService(
            client,
            new AlwaysExpandPolicy(),
            new Microsoft365QueryExpansionOptions(
                true,
                2,
                1500,
                1));

        // When
        var queries = await service.ExpandAsync(originalQuery, CancellationToken.None);

        // Then
        Assert.Equal(
            new[] { originalQuery.Trim(), firstVariant.Trim(), secondVariant.Trim() },
            queries);
    }

    [Theory, AutoDomainData]
    public async Task Given_QueryExpansionClientFails_When_ExpandAsync_Then_FallsBackToOriginalQuery(
        string query)
    {
        // Given
        var service = CreateService(
            new ThrowingQueryExpansionClient(),
            new AlwaysExpandPolicy(),
            new Microsoft365QueryExpansionOptions(
                true,
                3,
                1500,
                1));

        // When
        var queries = await service.ExpandAsync(query, CancellationToken.None);

        // Then
        Assert.Equal(new[] { query.Trim() }, queries);
    }

    [Theory, AutoDomainData]
    public async Task Given_QueryExpansionBypassPolicyRejectsQuery_When_ExpandAsync_Then_DoesNotCallClient(
        string query)
    {
        // Given
        var client = new RecordingQueryExpansionClient(["variant"]);
        var service = CreateService(
            client,
            new NeverExpandPolicy(),
            new Microsoft365QueryExpansionOptions(
                true,
                3,
                1500,
                1));

        // When
        var queries = await service.ExpandAsync(query, CancellationToken.None);

        // Then
        Assert.Equal(new[] { query.Trim() }, queries);
        Assert.Equal(0, client.CallCount);
    }

    [Theory, AutoDomainData]
    public void Given_QueryBelowMinimumWordCount_When_ShouldExpand_Then_ReturnsFalse()
    {
        // Given
        var policy = new Microsoft365QueryExpansionBypassPolicy(2);

        // When
        var shouldExpand = policy.ShouldExpand("ProjetX");

        // Then
        Assert.False(shouldExpand);
    }

    [Theory, AutoDomainData]
    public void Given_TwoWordQuery_When_ShouldExpand_Then_ReturnsTrue()
    {
        // Given
        var policy = new Microsoft365QueryExpansionBypassPolicy(2);

        // When
        var shouldExpand = policy.ShouldExpand("code projetx");

        // Then
        Assert.True(shouldExpand);
    }

    private static Microsoft365QueryExpansionService CreateService(
        IMicrosoft365QueryExpansionClient client,
        IMicrosoft365QueryExpansionBypassPolicy bypassPolicy,
        Microsoft365QueryExpansionOptions options) =>
        new(
            client,
            bypassPolicy,
            options,
            NullLogger<Microsoft365QueryExpansionService>.Instance);

    private sealed class RecordingQueryExpansionClient(
        IReadOnlyCollection<string> generatedQueries) : IMicrosoft365QueryExpansionClient
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyCollection<string>> GenerateAsync(
            string query,
            int maximumGeneratedQueries,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(generatedQueries);
        }
    }

    private sealed class ThrowingQueryExpansionClient : IMicrosoft365QueryExpansionClient
    {
        public Task<IReadOnlyCollection<string>> GenerateAsync(
            string query,
            int maximumGeneratedQueries,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Expansion failed.");
    }

    private sealed class AlwaysExpandPolicy : IMicrosoft365QueryExpansionBypassPolicy
    {
        public bool ShouldExpand(string query) => true;
    }

    private sealed class NeverExpandPolicy : IMicrosoft365QueryExpansionBypassPolicy
    {
        public bool ShouldExpand(string query) => false;
    }
}
