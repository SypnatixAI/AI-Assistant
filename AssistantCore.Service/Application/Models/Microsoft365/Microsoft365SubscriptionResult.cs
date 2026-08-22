namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365SubscriptionResult(
    string SubscriptionId,
    string Resource,
    DateTimeOffset ExpiresAt);
