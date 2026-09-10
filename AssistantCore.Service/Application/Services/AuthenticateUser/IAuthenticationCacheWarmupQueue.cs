namespace AssistantCore.Service.Application.Services.AuthenticateUser;

public interface IAuthenticationCacheWarmupQueue
{
    void TryQueue(
        Guid organizationId,
        string? externalTenantId,
        string entraUserId);
}
