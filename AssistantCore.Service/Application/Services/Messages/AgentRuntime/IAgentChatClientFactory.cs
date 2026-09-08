using AssistantCore.Service.Application.Models.Messages.AiModels;
using Microsoft.Extensions.AI;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

public interface IAgentChatClientFactory
{
    IChatClient Create(SelectedAiModel selectedModel);
}
