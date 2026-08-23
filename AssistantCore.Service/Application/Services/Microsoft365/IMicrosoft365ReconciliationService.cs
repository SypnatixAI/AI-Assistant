namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ReconciliationService
{
    Task RunReconciliationAsync(CancellationToken cancellationToken = default);
}
