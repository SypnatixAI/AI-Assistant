namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365SubscriptionRenewalResult(
    bool Exists,
    Microsoft365SubscriptionResult? Subscription)
{
    public static Microsoft365SubscriptionRenewalResult NotFound { get; } = new(false, null);
}
