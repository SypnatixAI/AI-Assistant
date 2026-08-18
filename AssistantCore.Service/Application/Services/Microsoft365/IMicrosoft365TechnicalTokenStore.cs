namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365TechnicalTokenStore
{
    Task StoreAsync(
        Guid connectionId,
        string accessToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<string?> GetAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);
}
