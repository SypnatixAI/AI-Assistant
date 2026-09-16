using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365OnboardingCompletionChecker(
    IMicrosoft365ConnectionRepository connectionRepository,
    IMemoryCache memoryCache)
    : IMicrosoft365OnboardingCompletionChecker
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<bool> IsCompleteAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(organizationId);
        if (memoryCache.TryGetValue(cacheKey, out bool cachedIsComplete))
        {
            return cachedIsComplete;
        }

        var connection = await connectionRepository.FindByOrganizationAsync(
            organizationId,
            cancellationToken);
        var isComplete = connection?.Status == Microsoft365ConnectionStatus.Active;
        memoryCache.Set(cacheKey, isComplete, CacheDuration);
        return isComplete;
    }

    private static string GetCacheKey(Guid organizationId) =>
        $"m365-onboarding-complete:{organizationId:D}";
}
