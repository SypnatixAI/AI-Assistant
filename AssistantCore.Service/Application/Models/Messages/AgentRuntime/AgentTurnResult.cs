using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Application.Models.Messages.AgentRuntime;

public sealed record AgentTurnResult(
    string Content,
    string ModelName,
    IReadOnlyCollection<RetrievedEvidence> Citations,
    IReadOnlyCollection<string> Warnings,
    AgentTurnUsage Usage);
