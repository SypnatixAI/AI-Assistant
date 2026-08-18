using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365TechnicalTokenStoreAdapter(
    IMemoryCache memoryCache,
    IDataProtectionProvider dataProtectionProvider) : IMicrosoft365TechnicalTokenStore
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "AssistantCore.Microsoft365.TechnicalToken.v1");

    public Task StoreAsync(
        Guid connectionId,
        string accessToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Set(GetCacheKey(connectionId), protector.Protect(accessToken), expiresAt);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = memoryCache.TryGetValue<string>(GetCacheKey(connectionId), out var protectedToken)
            ? protector.Unprotect(protectedToken!)
            : null;
        return Task.FromResult(token);
    }

    public Task RemoveAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        memoryCache.Remove(GetCacheKey(connectionId));
        return Task.CompletedTask;
    }

    private static string GetCacheKey(Guid connectionId) => $"m365-token:{connectionId:D}";
}
