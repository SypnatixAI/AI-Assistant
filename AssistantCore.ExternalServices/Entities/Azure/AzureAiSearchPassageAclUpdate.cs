namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchPassageAclUpdate(
    string ChunkId,
    IReadOnlyCollection<string> AllowedUserIds,
    IReadOnlyCollection<string> AllowedGroupIds,
    IReadOnlyCollection<string> AllowedSharePointGroupIds,
    bool HasAnonymousLink,
    string AclFingerprint);
