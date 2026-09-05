using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365UserGroupResolverAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphUserGroupClient groupClient,
    IMicrosoft365SecurityIdentityNormalizer identityNormalizer,
    IOptions<Microsoft365Options> options) : IMicrosoft365UserGroupResolver
{
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

        return groupIds
            .Concat(ownedGroupIds.Select(identityNormalizer.NormalizeEntraGroupOwnerId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(groupId => groupId, StringComparer.Ordinal)
            .ToArray();
    }
}
