namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ApplicationTokenClient
{
    Task<string> AcquireGraphTokenAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
