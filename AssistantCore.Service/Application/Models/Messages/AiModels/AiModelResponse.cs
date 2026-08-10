using AssistantCore.Service.Application.Models.Messages.Orchestration;

namespace AssistantCore.Service.Application.Models.Messages.AiModels;

public sealed record AiModelResponse(
    AiModelNextAction NextAction,
    AiModelUsage Usage);
