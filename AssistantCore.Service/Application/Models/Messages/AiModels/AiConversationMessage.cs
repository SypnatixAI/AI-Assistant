namespace AssistantCore.Service.Application.Models.Messages.AiModels;

public sealed record AiConversationMessage(
    AiConversationRole Role,
    string Content);
