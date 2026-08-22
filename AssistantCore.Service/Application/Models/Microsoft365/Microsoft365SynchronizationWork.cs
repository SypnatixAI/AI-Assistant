namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365SynchronizationWork(
    Guid WorkId,
    string WorkType,
    string SubscriptionId,
    string? SiteId,
    string? ListId,
    string? DriveId);
