namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ConsentExchange(
    string TenantId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);
