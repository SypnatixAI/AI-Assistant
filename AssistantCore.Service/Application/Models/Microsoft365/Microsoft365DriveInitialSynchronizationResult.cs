namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365DriveInitialSynchronizationResult(
    Microsoft365DriveInitialSynchronizationStatus Status,
    int ProcessWorkCount,
    int DeleteWorkCount,
    int IgnoredFolderCount,
    int IgnoredUnsupportedFileCount,
    int PersistedWorkCount,
    string? DeltaLink);
