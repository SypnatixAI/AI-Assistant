using System.Collections.Concurrent;
using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Evidence;
using AssistantCore.Service.Application.Services.Messages.Tools;
using AssistantCore.Service.Infrastructure.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Tests.Integration;

public sealed class ConnectorServiceCollectionExtensionsTests
{
    [Theory, AutoDomainData]
    public void Given_ValidConnectorConfiguration_When_AddConnectorInfrastructure_Then_RegistersSharedInfrastructure(
        Guid _)
    {
        // Given
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Connectors:Microsoft365:MaximumResults"] = "10",
                ["Connectors:Microsoft365:MaximumContentLength"] = "4000"
            })
            .Build();
        var services = new ServiceCollection();

        // When
        services.AddConnectorInfrastructure(configuration);

        // Then
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEvidenceNormalizer>());
        Assert.IsType<ScopedToolExecutionRouter>(
            scope.ServiceProvider.GetRequiredService<IToolExecutionRouter>());
    }

    [Theory, AutoDomainData]
    public async Task Given_ParallelToolCalls_When_ExecuteAsync_Then_UsesAnIndependentScopePerCall(
        Guid organizationId,
        Guid memberId)
    {
        // Given
        var observedScopeIds = new ConcurrentBag<Guid>();
        var parallelExecutionGate = new ParallelExecutionGate();
        var services = new ServiceCollection();
        services.AddSingleton(observedScopeIds);
        services.AddSingleton(parallelExecutionGate);
        services.AddScoped<ToolExecutionScopeMarker>();
        services.AddScoped<IAiToolExecutionHandler, ScopedRecordingToolExecutionHandler>();
        services.AddScoped<IToolExecutionRouter, ScopedToolExecutionRouter>();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var requestScope = serviceProvider.CreateAsyncScope();
        var router = requestScope.ServiceProvider.GetRequiredService<IToolExecutionRouter>();
        var executionContext = new ConnectorExecutionContext(organizationId, memberId);
        var calls = Enumerable.Range(1, 2)
            .Select(index => new ValidatedToolCall(
                $"call-{index}",
                ScopedRecordingToolExecutionHandler.ToolName,
                JsonSerializer.SerializeToElement(new { })))
            .ToArray();

        // When
        var results = await Task.WhenAll(calls.Select(call => router.ExecuteAsync(
            call,
            executionContext,
            CancellationToken.None)));

        // Then
        Assert.All(results, result => Assert.Equal(ToolExecutionStatus.Success, result.Status));
        Assert.Equal(2, observedScopeIds.Distinct().Count());
    }

    private sealed class ToolExecutionScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class ScopedRecordingToolExecutionHandler(
        ToolExecutionScopeMarker scopeMarker,
        ConcurrentBag<Guid> observedScopeIds,
        ParallelExecutionGate parallelExecutionGate) : IAiToolExecutionHandler
    {
        public const string ToolName = "record_scope";

        string IAiToolExecutionHandler.ToolName => ToolName;

        public async Task<ToolExecutionResult> ExecuteAsync(
            ValidatedToolCall validatedToolCall,
            ConnectorExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            observedScopeIds.Add(scopeMarker.Id);
            await parallelExecutionGate.WaitForBothCallsAsync(cancellationToken);

            return ToolExecutionResult.Succeeded(
                validatedToolCall.CallId,
                []);
        }
    }

    private sealed class ParallelExecutionGate
    {
        private readonly TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int enteredCallCount;

        public async Task WaitForBothCallsAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref enteredCallCount) == 2)
            {
                completion.TrySetResult();
            }

            await completion.Task.WaitAsync(cancellationToken);
        }
    }
}
