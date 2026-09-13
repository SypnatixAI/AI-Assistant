using AssistantCore.Service.Application.Models.Messages.AiModels;

namespace AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;

public sealed record AgenticRetrievalMessage(
    AiConversationRole Role,
    string Content);
