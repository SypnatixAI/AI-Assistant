namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365AclReconciliationService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
