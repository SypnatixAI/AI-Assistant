namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiConversationMessage(
    OpenAiConversationRole Role,
    string Content);
