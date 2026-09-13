using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365UserGroupResolverAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphUserGroupClient groupClient,
    IMicrosoft365SecurityIdentityNormalizer identityNormalizer,
    IOptions<Microsoft365Options> options,
    IMemoryCache? memoryCache = null) : IMicrosoft365UserGroupResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
        string externalTenantId,
        string entraUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalTenantId))
        {
            throw new InvalidOperationException(
                "The authenticated organization has no Microsoft Entra tenant identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(entraUserId);

        var cacheKey = new EntraGroupCacheKey(
            externalTenantId.Trim().ToLowerInvariant(),
            entraUserId.Trim().ToLowerInvariant());
        if (memoryCache is not null
            && memoryCache.TryGetValue(
                cacheKey,
                out IReadOnlyCollection<string>? cachedGroupIds)
            && cachedGroupIds is not null)
        {
            return cachedGroupIds;
        }

        var configuration = options.Value;
        var token = await identityClient.AcquireApplicationTokenAsync(
            configuration.AuthorityBaseUrl,
            externalTenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            cancellationToken);
        var groupIdsTask = groupClient.GetTransitiveGroupIdsAsync(
            configuration.GraphBaseUrl,
            token.AccessToken,
            entraUserId,
            cancellationToken);
        var ownedGroupIdsTask = groupClient.GetOwnedGroupIdsAsync(
            configuration.GraphBaseUrl,
            token.AccessToken,
            entraUserId,
            cancellationToken);
        await Task.WhenAll(groupIdsTask, ownedGroupIdsTask);
        var groupIds = await groupIdsTask;
        var ownedGroupIds = await ownedGroupIdsTask;

        var resolvedGroupIds = groupIds
            .Concat(ownedGroupIds.Select(identityNormalizer.NormalizeEntraGroupOwnerId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(groupId => groupId, StringComparer.Ordinal)
            .ToArray();

        memoryCache?.Set(cacheKey, resolvedGroupIds, CacheDuration);
        return resolvedGroupIds;
    }

    private sealed record EntraGroupCacheKey(
        string TenantId,
        string UserId);
}
