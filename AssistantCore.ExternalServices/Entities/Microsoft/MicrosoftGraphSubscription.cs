namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftGraphSubscription(
    string Id,
    string Resource,
    DateTimeOffset ExpiresAt);
