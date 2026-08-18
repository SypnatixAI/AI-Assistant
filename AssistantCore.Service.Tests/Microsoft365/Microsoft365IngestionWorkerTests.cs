using AssistantCore.Ingestion.Worker;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365IngestionWorkerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AStartupConnection_When_StartAsync_Then_ConnectionIsChecked(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        // Given
        var orchestrator = new RecordingIngestionOrchestrator();
        var services = new ServiceCollection()
            .AddSingleton<IMicrosoft365IngestionOrchestrator>(orchestrator)
            .BuildServiceProvider();
        var worker = new Microsoft365IngestionWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new Microsoft365WorkerOptions
            {
                RunStartupConnectionCheck = true,
                StartupConnectionId = connectionId
            }),
            NullLogger<Microsoft365IngestionWorker>.Instance);

        // When
        await worker.StartAsync(cancellationToken);
        await worker.StopAsync(cancellationToken);

        // Then
        Assert.Equal(connectionId, orchestrator.ConnectionId);
    }

    private sealed class RecordingIngestionOrchestrator : IMicrosoft365IngestionOrchestrator
    {
        public Guid? ConnectionId { get; private set; }

        public Task ScheduleInitialSynchronizationAsync(
            Guid connectionId,
            CancellationToken cancellationToken = default)
        {
            ConnectionId = connectionId;
            return Task.CompletedTask;
        }
    }
}
