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
        Guid connectionId)
    {
        // Given
        var orchestrator = new RecordingIngestionOrchestrator();
        var maintenanceService = new RecordingSubscriptionMaintenanceService();
        var services = new ServiceCollection()
            .AddSingleton<IMicrosoft365IngestionOrchestrator>(orchestrator)
            .AddSingleton<IMicrosoft365SubscriptionMaintenanceService>(maintenanceService)
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
        await worker.StartAsync(CancellationToken.None);
        await orchestrator.WaitUntilScheduledAsync().WaitAsync(TimeSpan.FromSeconds(1));
        await worker.StopAsync(CancellationToken.None);

        // Then
        Assert.Equal(connectionId, orchestrator.ConnectionId);
    }

    [Theory, AutoDomainData]
    public async Task Given_TheWorkerStarts_When_RunMaintenanceAsync_Then_SubscriptionsAreProcessed()
    {
        // Given
        var maintenanceService = new RecordingSubscriptionMaintenanceService();
        var services = new ServiceCollection()
            .AddSingleton<IMicrosoft365SubscriptionMaintenanceService>(maintenanceService)
            .BuildServiceProvider();
        var worker = new Microsoft365IngestionWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new Microsoft365WorkerOptions
            {
                RunStartupConnectionCheck = false,
                MaintenanceIntervalSeconds = 300
            }),
            NullLogger<Microsoft365IngestionWorker>.Instance);

        // When
        await worker.StartAsync(CancellationToken.None);
        await maintenanceService.WaitUntilRunAsync().WaitAsync(TimeSpan.FromSeconds(1));
        await worker.StopAsync(CancellationToken.None);

        // Then
        Assert.Equal(1, maintenanceService.CallCount);
    }

    private sealed class RecordingIngestionOrchestrator : IMicrosoft365IngestionOrchestrator
    {
        private readonly TaskCompletionSource scheduled = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Guid? ConnectionId { get; private set; }

        public Task WaitUntilScheduledAsync() => scheduled.Task;

        public Task ScheduleInitialSynchronizationAsync(
            Guid connectionId,
            CancellationToken cancellationToken = default)
        {
            ConnectionId = connectionId;
            scheduled.SetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSubscriptionMaintenanceService
        : IMicrosoft365SubscriptionMaintenanceService
    {
        private readonly TaskCompletionSource ran = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public Task WaitUntilRunAsync() => ran.Task;

        public Task RunMaintenanceAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            ran.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
