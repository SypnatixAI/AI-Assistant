namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftAuthorizationCodeToken(
    string AccessToken,
    int ExpiresInSeconds);
