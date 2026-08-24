namespace AssistantCore.Service.Application.Models.Microsoft365.Permissions;

public abstract record Microsoft365AclResolution
{
    private Microsoft365AclResolution()
    {
    }

    public sealed record ResolvedAcl : Microsoft365AclResolution
    {
        public ResolvedAcl(Microsoft365Acl acl)
        {
            Acl = acl ?? throw new ArgumentNullException(nameof(acl));
        }

        public Microsoft365Acl Acl { get; }
    }

    public sealed record Unresolved(Microsoft365AclResolutionFailureReason Reason) : Microsoft365AclResolution;
}
