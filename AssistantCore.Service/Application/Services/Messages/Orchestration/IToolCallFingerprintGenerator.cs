using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public interface IToolCallFingerprintGenerator
{
    string CreateFingerprint(AiRequestedToolCall toolCall);
}
