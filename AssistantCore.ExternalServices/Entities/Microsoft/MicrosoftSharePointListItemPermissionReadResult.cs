namespace AssistantCore.ExternalServices.Entities.Microsoft;

public abstract record MicrosoftSharePointListItemPermissionReadResult
{
    private MicrosoftSharePointListItemPermissionReadResult()
    {
    }

    public sealed record Resolved(
        MicrosoftSharePointPermissionInheritanceSource InheritanceSource,
        IReadOnlyCollection<MicrosoftSharePointPermission> Permissions)
        : MicrosoftSharePointListItemPermissionReadResult;

    public sealed record Unresolved(MicrosoftSharePointPermissionUnresolvedReason Reason)
        : MicrosoftSharePointListItemPermissionReadResult;
}
