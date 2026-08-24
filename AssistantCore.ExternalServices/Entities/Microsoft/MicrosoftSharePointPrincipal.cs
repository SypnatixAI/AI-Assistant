namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftSharePointPrincipal(
    int Id,
    string? EntraObjectId,
    string Title,
    string? LoginName,
    int PrincipalType);
