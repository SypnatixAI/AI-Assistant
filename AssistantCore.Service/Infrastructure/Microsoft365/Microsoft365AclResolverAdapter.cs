using System.Net;
using AssistantCore.ExternalServices.Entities.Microsoft;
using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365AclResolverAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphDriveItemPermissionClient graphPermissionClient,
    MicrosoftSharePointListItemPermissionClient sharePointPermissionClient,
    IMicrosoft365SecurityIdentityNormalizer identityNormalizer,
    IMicrosoft365PermissionRoleEvaluator roleEvaluator,
    IOptions<Microsoft365Options> options,
    ILogger<Microsoft365AclResolverAdapter> logger) : IMicrosoft365AclResolver
{
    public async Task<Microsoft365AclResolution> ResolveAsync(
        Organization organization,
        Microsoft365ContentReference contentReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(contentReference);

        if (string.IsNullOrWhiteSpace(organization.ExternalTenantId))
        {
            return Unresolved(
                organization.Id,
                contentReference.Kind,
                Microsoft365AclResolutionFailureReason.UnsupportedPermission);
        }

        try
        {
            var resolution = contentReference.Kind switch
            {
                Microsoft365ContentReferenceKind.DriveItem => await ResolveDriveItemAsync(
                    organization.ExternalTenantId,
                    contentReference,
                    cancellationToken),
                Microsoft365ContentReferenceKind.ListItem => await ResolveListItemAsync(
                    organization.ExternalTenantId,
                    contentReference,
                    cancellationToken),
                _ => new Microsoft365AclResolution.Unresolved(
                    Microsoft365AclResolutionFailureReason.UnsupportedPermission)
            };

            return resolution is Microsoft365AclResolution.Unresolved unresolved
                ? Unresolved(organization.Id, contentReference.Kind, unresolved.Reason)
                : resolution;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unresolved(
                organization.Id,
                contentReference.Kind,
                Microsoft365AclResolutionFailureReason.Timeout);
        }
        catch (MicrosoftExternalException exception)
        {
            return Unresolved(
                organization.Id,
                contentReference.Kind,
                MapExternalFailure(exception));
        }
        catch (ArgumentException)
        {
            return Unresolved(
                organization.Id,
                contentReference.Kind,
                Microsoft365AclResolutionFailureReason.UnsupportedPermission);
        }
    }

    private async Task<Microsoft365AclResolution> ResolveDriveItemAsync(
        string tenantId,
        Microsoft365ContentReference contentReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contentReference.DriveId))
        {
            return new Microsoft365AclResolution.Unresolved(
                Microsoft365AclResolutionFailureReason.UnsupportedPermission);
        }

        var configuration = options.Value;
        var token = await identityClient.AcquireApplicationTokenAsync(
            configuration.AuthorityBaseUrl,
            tenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            cancellationToken);
        var permissions = await graphPermissionClient.GetPermissionsAsync(
            configuration.GraphBaseUrl,
            token.AccessToken,
            contentReference.DriveId,
            contentReference.ItemId,
            cancellationToken);

        if (permissions.Count == 0)
        {
            return new Microsoft365AclResolution.Unresolved(
                Microsoft365AclResolutionFailureReason.PartialResponse);
        }

        var accumulator = new AclAccumulator();
        foreach (var permission in permissions)
        {
            var roleEvaluation = roleEvaluator.EvaluateDriveItemRoles(permission.Roles);
            if (roleEvaluation == Microsoft365PermissionRoleEvaluation.Unresolved)
            {
                return new Microsoft365AclResolution.Unresolved(
                    Microsoft365AclResolutionFailureReason.UnsupportedPermission);
            }

            if (roleEvaluation == Microsoft365PermissionRoleEvaluation.NoReadAccess)
            {
                continue;
            }

            if (permission.Link is not null)
            {
                if (!string.Equals(
                        permission.Link.Scope,
                        "anonymous",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new Microsoft365AclResolution.Unresolved(
                        Microsoft365AclResolutionFailureReason.UnsupportedPermission);
                }

                accumulator.HasAnonymousLink = true;
                continue;
            }

            var identities = permission.GrantedToIdentitiesV2.ToList();
            if (permission.GrantedToV2 is not null)
            {
                identities.Add(permission.GrantedToV2);
            }

            if (identities.Count == 0
                || identities.Any(identity => !TryAddDriveIdentity(
                    contentReference.SiteId,
                    identity,
                    accumulator)))
            {
                return new Microsoft365AclResolution.Unresolved(
                    Microsoft365AclResolutionFailureReason.UnknownPrincipal);
            }

            if (permission.InheritedFrom is null)
            {
                accumulator.HasUniquePermissions = true;
            }
        }

        return accumulator.CreateResolution();
    }

    private async Task<Microsoft365AclResolution> ResolveListItemAsync(
        string tenantId,
        Microsoft365ContentReference contentReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contentReference.SiteUrl)
            || !Guid.TryParse(contentReference.ListId, out var listId)
            || !int.TryParse(contentReference.ItemId, out var itemId)
            || itemId <= 0)
        {
            return new Microsoft365AclResolution.Unresolved(
                Microsoft365AclResolutionFailureReason.UnsupportedPermission);
        }

        var siteUri = new Uri(contentReference.SiteUrl, UriKind.Absolute);
        var configuration = options.Value;
        var token = await identityClient.AcquireApplicationTokenForScopeAsync(
            configuration.AuthorityBaseUrl,
            tenantId,
            configuration.ClientId,
            configuration.ClientSecret,
            $"{siteUri.GetLeftPart(UriPartial.Authority)}/.default",
            cancellationToken);
        var permissionResult = await sharePointPermissionClient.GetPermissionsAsync(
            contentReference.SiteUrl,
            token.AccessToken,
            listId,
            itemId,
            cancellationToken);

        if (permissionResult is MicrosoftSharePointListItemPermissionReadResult.Unresolved unresolved)
        {
            return new Microsoft365AclResolution.Unresolved(unresolved.Reason switch
            {
                MicrosoftSharePointPermissionUnresolvedReason.UnknownPrincipal =>
                    Microsoft365AclResolutionFailureReason.UnknownPrincipal,
                _ => Microsoft365AclResolutionFailureReason.PartialResponse
            });
        }

        var resolved = (MicrosoftSharePointListItemPermissionReadResult.Resolved)permissionResult;
        var accumulator = new AclAccumulator
        {
            HasUniquePermissions = resolved.InheritanceSource
                == MicrosoftSharePointPermissionInheritanceSource.ListItem
        };

        foreach (var permission in resolved.Permissions)
        {
            var roleEvaluation = roleEvaluator.EvaluateSharePointRoleTypes(
                permission.RoleDefinitions.Select(role => role.RoleTypeKind).ToArray());
            if (roleEvaluation == Microsoft365PermissionRoleEvaluation.Unresolved)
            {
                return new Microsoft365AclResolution.Unresolved(
                    Microsoft365AclResolutionFailureReason.UnsupportedPermission);
            }

            if (roleEvaluation == Microsoft365PermissionRoleEvaluation.NoReadAccess)
            {
                continue;
            }

            if (!TryAddSharePointPrincipal(
                    contentReference.SiteId,
                    permission.Principal,
                    accumulator))
            {
                return new Microsoft365AclResolution.Unresolved(
                    Microsoft365AclResolutionFailureReason.UnknownPrincipal);
            }
        }

        return accumulator.CreateResolution();
    }

    private bool TryAddDriveIdentity(
        string siteId,
        MicrosoftDriveItemPermissionIdentitySet identity,
        AclAccumulator accumulator)
    {
        if (identity.User is not null && identity.Group is not null)
        {
            return false;
        }

        if (identity.User is not null)
        {
            return TryNormalize(
                () => identityNormalizer.NormalizeEntraUserId(identity.User.Id!),
                accumulator.EntraUserIds);
        }

        if (identity.Group is not null)
        {
            return TryNormalize(
                () => identityNormalizer.NormalizeEntraGroupId(identity.Group.Id!),
                accumulator.EntraGroupIds);
        }

        var sharePointGroup = identity.SiteGroup ?? identity.SharePointGroup;
        if (sharePointGroup is not null)
        {
            return TryNormalize(
                () => identityNormalizer.NormalizeSharePointGroupId(siteId, sharePointGroup.Id!),
                accumulator.SharePointGroupIds);
        }

        return false;
    }

    private bool TryAddSharePointPrincipal(
        string siteId,
        MicrosoftSharePointPrincipal principal,
        AclAccumulator accumulator) => principal.PrincipalType switch
        {
            1 => TryNormalize(
                () => identityNormalizer.NormalizeEntraUserId(principal.EntraObjectId!),
                accumulator.EntraUserIds),
            4 => TryNormalize(
                () => identityNormalizer.NormalizeEntraGroupId(principal.EntraObjectId!),
                accumulator.EntraGroupIds),
            8 => TryNormalize(
                () => identityNormalizer.NormalizeSharePointGroupId(
                    siteId,
                    principal.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                accumulator.SharePointGroupIds),
            _ => false
        };

    private static bool TryNormalize(Func<string> normalize, ICollection<string> destination)
    {
        try
        {
            destination.Add(normalize());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private Microsoft365AclResolution.Unresolved Unresolved(
        Guid organizationId,
        Microsoft365ContentReferenceKind contentKind,
        Microsoft365AclResolutionFailureReason reason)
    {
        logger.LogWarning(
            "Microsoft 365 ACL resolution failed for organization {OrganizationId}, content kind {ContentKind}, reason {FailureReason}.",
            organizationId,
            contentKind,
            reason);
        return new Microsoft365AclResolution.Unresolved(reason);
    }

    private static Microsoft365AclResolutionFailureReason MapExternalFailure(
        MicrosoftExternalException exception) => exception.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                Microsoft365AclResolutionFailureReason.AccessDenied,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
                Microsoft365AclResolutionFailureReason.Timeout,
            _ => Microsoft365AclResolutionFailureReason.PartialResponse
        };

    private sealed class AclAccumulator
    {
        public List<string> EntraUserIds { get; } = [];

        public List<string> EntraGroupIds { get; } = [];

        public List<string> SharePointGroupIds { get; } = [];

        public bool HasAnonymousLink { get; set; }

        public bool HasUniquePermissions { get; set; }

        public Microsoft365AclResolution CreateResolution() =>
            new Microsoft365AclResolution.ResolvedAcl(new Microsoft365Acl(
                EntraUserIds,
                EntraGroupIds,
                SharePointGroupIds,
                HasAnonymousLink,
                HasUniquePermissions
                    ? Microsoft365AclInheritance.Unique
                    : Microsoft365AclInheritance.Inherited));
    }
}
