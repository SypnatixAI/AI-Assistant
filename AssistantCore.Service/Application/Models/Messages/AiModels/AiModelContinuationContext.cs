namespace AssistantCore.Service.Application.Models.Messages.AiModels;

public sealed record AiModelContinuationContext(
    string Provider,
    string Token);
