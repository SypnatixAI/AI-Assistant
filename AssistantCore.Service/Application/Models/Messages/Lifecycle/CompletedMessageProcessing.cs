using AssistantCore.Service.Application.Models.Usage;

namespace AssistantCore.Service.Application.Models.Messages.Lifecycle;

public sealed record CompletedMessageProcessing(
    Guid AssistantMessageId,
    DateTimeOffset CreatedAt,
    MessageUsageResponse Usage);
