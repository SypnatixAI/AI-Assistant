namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SubscriptionMaintenanceService
{
    Task RunMaintenanceAsync(CancellationToken cancellationToken = default);
}
