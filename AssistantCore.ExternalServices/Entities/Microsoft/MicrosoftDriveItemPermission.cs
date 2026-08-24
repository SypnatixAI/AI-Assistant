namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDriveItemPermission(
    string Id,
    IReadOnlyCollection<string> Roles,
    MicrosoftDriveItemPermissionIdentitySet? GrantedToV2,
    IReadOnlyCollection<MicrosoftDriveItemPermissionIdentitySet> GrantedToIdentitiesV2,
    MicrosoftDriveItemPermissionItemReference? InheritedFrom,
    MicrosoftDriveItemSharingLink? Link);
