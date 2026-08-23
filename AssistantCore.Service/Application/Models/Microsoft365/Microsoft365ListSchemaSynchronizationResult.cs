namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ListSchemaSynchronizationResult(
    Microsoft365ListSchemaSynchronizationStatus Status,
    string? SchemaFingerprint,
    bool RequiresItemReprocessing);
