namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ListInitialSynchronizationResult(
    Microsoft365ListInitialSynchronizationStatus Status,
    int ProcessWorkCount,
    int DeleteWorkCount,
    int PersistedWorkCount,
    string? DeltaLink);
