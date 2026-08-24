namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftSharePointPermission(
    MicrosoftSharePointPrincipal Principal,
    IReadOnlyCollection<MicrosoftSharePointRoleDefinition> RoleDefinitions);
