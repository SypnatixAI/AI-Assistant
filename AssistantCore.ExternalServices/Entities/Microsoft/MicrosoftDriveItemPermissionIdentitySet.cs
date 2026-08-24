namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftDriveItemPermissionIdentitySet(
    MicrosoftDriveItemPermissionIdentity? User,
    MicrosoftDriveItemPermissionIdentity? Group,
    MicrosoftDriveItemPermissionIdentity? SiteUser,
    MicrosoftDriveItemPermissionIdentity? SiteGroup,
    MicrosoftDriveItemPermissionIdentity? SharePointGroup);
