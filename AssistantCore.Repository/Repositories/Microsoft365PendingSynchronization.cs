using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories;

public sealed record Microsoft365PendingSynchronization(
    Guid SynchronizationId,
    Guid SourceId,
    Microsoft365SynchronizationType Type);
