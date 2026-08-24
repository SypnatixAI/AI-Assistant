namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureAiSearchPassageDocument(
    string ChunkId,
    string OrganizationId,
    string Title,
    string Content,
    IReadOnlyCollection<string> AllowedUserIds,
    IReadOnlyCollection<string> AllowedGroupIds,
    IReadOnlyCollection<string> AllowedSharePointGroupIds,
    bool HasAnonymousLink,
    string AclFingerprint,
    bool IsAvailable);
