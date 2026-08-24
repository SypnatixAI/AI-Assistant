using System.Text.Json.Serialization;
using AssistantCore.ExternalServices.Entities.Microsoft;

namespace AssistantCore.ExternalServices.Services.Microsoft;

public sealed class MicrosoftGraphDriveItemPermissionClient(HttpClient httpClient)
{
    private readonly MicrosoftGraphCollectionReader collectionReader = new(httpClient);

    public Task<IReadOnlyCollection<MicrosoftDriveItemPermission>> GetPermissionsAsync(
        string graphBaseUrl,
        string accessToken,
        string driveId,
        string driveItemId,
        CancellationToken cancellationToken = default) =>
        collectionReader.ReadAsync<Permission, MicrosoftDriveItemPermission>(
            CreatePermissionsUri(graphBaseUrl, driveId, driveItemId),
            accessToken,
            MapPermission,
            "drive item permissions",
            cancellationToken);

    private static Uri CreatePermissionsUri(
        string graphBaseUrl,
        string driveId,
        string driveItemId)
    {
        if (string.IsNullOrWhiteSpace(driveId))
        {
            throw new ArgumentException("Microsoft Graph drive identifier is required.", nameof(driveId));
        }

        if (string.IsNullOrWhiteSpace(driveItemId))
        {
            throw new ArgumentException("Microsoft Graph drive item identifier is required.", nameof(driveItemId));
        }

        if (!Uri.TryCreate(graphBaseUrl, UriKind.Absolute, out var graphBaseUri)
            || graphBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Microsoft Graph base URL must use HTTPS.", nameof(graphBaseUrl));
        }

        var normalizedBaseUri = new Uri($"{graphBaseUri.GetLeftPart(UriPartial.Authority)}/");
        return new Uri(
            normalizedBaseUri,
            $"v1.0/drives/{Uri.EscapeDataString(driveId)}/items/{Uri.EscapeDataString(driveItemId)}/permissions");
    }

    private static MicrosoftDriveItemPermission MapPermission(Permission permission)
    {
        if (string.IsNullOrWhiteSpace(permission.Id))
        {
            throw new MicrosoftExternalException(
                "Microsoft Graph drive item permissions response contained an invalid permission.");
        }

        return new MicrosoftDriveItemPermission(
            permission.Id,
            permission.Roles?.Where(role => !string.IsNullOrWhiteSpace(role)).ToArray() ?? [],
            MapIdentitySet(permission.GrantedToV2),
            permission.GrantedToIdentitiesV2?.Select(MapIdentitySet).Where(identitySet => identitySet is not null)
                .Select(identitySet => identitySet!)
                .ToArray() ?? [],
            MapItemReference(permission.InheritedFrom),
            MapSharingLink(permission.Link));
    }

    private static MicrosoftDriveItemPermissionIdentitySet? MapIdentitySet(SharePointIdentitySet? identitySet)
    {
        if (identitySet is null)
        {
            return null;
        }

        return new MicrosoftDriveItemPermissionIdentitySet(
            MapIdentity(identitySet.User),
            MapIdentity(identitySet.Group),
            MapIdentity(identitySet.SiteUser),
            MapIdentity(identitySet.SiteGroup),
            MapIdentity(identitySet.SharePointGroup));
    }

    private static MicrosoftDriveItemPermissionIdentity? MapIdentity(Identity? identity)
    {
        if (identity is null)
        {
            return null;
        }

        return new MicrosoftDriveItemPermissionIdentity(
            identity.Id,
            identity.DisplayName,
            identity.LoginName);
    }

    private static MicrosoftDriveItemPermissionItemReference? MapItemReference(ItemReference? itemReference)
    {
        if (itemReference is null)
        {
            return null;
        }

        return new MicrosoftDriveItemPermissionItemReference(
            itemReference.DriveId,
            itemReference.Id,
            itemReference.Path);
    }

    private static MicrosoftDriveItemSharingLink? MapSharingLink(SharingLink? link)
    {
        if (link is null)
        {
            return null;
        }

        return new MicrosoftDriveItemSharingLink(
            link.Type,
            link.Scope,
            link.WebUrl,
            link.PreventsDownload,
            MapIdentity(link.Application));
    }

    private sealed record Permission(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("roles")] IReadOnlyCollection<string>? Roles,
        [property: JsonPropertyName("grantedToV2")] SharePointIdentitySet? GrantedToV2,
        [property: JsonPropertyName("grantedToIdentitiesV2")]
        IReadOnlyCollection<SharePointIdentitySet>? GrantedToIdentitiesV2,
        [property: JsonPropertyName("inheritedFrom")] ItemReference? InheritedFrom,
        [property: JsonPropertyName("link")] SharingLink? Link);

    private sealed record SharePointIdentitySet(
        [property: JsonPropertyName("user")] Identity? User,
        [property: JsonPropertyName("group")] Identity? Group,
        [property: JsonPropertyName("siteUser")] Identity? SiteUser,
        [property: JsonPropertyName("siteGroup")] Identity? SiteGroup,
        [property: JsonPropertyName("sharePointGroup")] Identity? SharePointGroup);

    private sealed record Identity(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("loginName")] string? LoginName);

    private sealed record ItemReference(
        [property: JsonPropertyName("driveId")] string? DriveId,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("path")] string? Path);

    private sealed record SharingLink(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("scope")] string? Scope,
        [property: JsonPropertyName("webUrl")] string? WebUrl,
        [property: JsonPropertyName("preventsDownload")] bool? PreventsDownload,
        [property: JsonPropertyName("application")] Identity? Application);
}
