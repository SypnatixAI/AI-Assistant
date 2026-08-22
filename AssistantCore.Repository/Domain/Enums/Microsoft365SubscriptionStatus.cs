namespace AssistantCore.Repository.Domain.Enums;

public enum Microsoft365SubscriptionStatus
{
    Pending,
    Active,
    RenewalRequired,
    RevocationRequired,
    Error,
    Revoked,
    Expired
}
