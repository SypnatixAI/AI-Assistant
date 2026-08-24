using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365UserGroupResolverAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphUserGroupClient groupClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365UserGroupResolver
{
    public async Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
        Organization organization,
        string entraUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organization);
        if (string.IsNullOrWhiteSpace(organization.ExternalTenantId))
        {
            throw new InvalidOperationException(
                "The authenticated organization has no Microsoft Entra tenant identifier.");
        }

        var configuration = options.Value;
        var token = await identityClient.AcquireApplicationTokenAsync(
            configuration.AuthorityBaseUrl,
            organization.ExternalTenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            cancellationToken);
        var groupIds = await groupClient.GetTransitiveGroupIdsAsync(
            configuration.GraphBaseUrl,
            token.AccessToken,
            entraUserId,
            cancellationToken);

        return groupIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(groupId => groupId, StringComparer.Ordinal)
            .ToArray();
    }
}
