namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DelegatedTokenProvider
{
    Task<string> GetGraphAccessTokenAsync(
        string tenantId,
        CancellationToken cancellationToken);
}
