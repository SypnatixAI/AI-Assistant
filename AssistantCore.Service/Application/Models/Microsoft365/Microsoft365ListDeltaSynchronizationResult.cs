namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ListDeltaSynchronizationResult(
    Microsoft365ListDeltaSynchronizationStatus Status,
    int ProcessWorkCount,
    int DeleteWorkCount,
    int PersistedWorkCount,
    string? DeltaLink);
