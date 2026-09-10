using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Lifecycle;

namespace AssistantCore.Service.Application.Models.Messages.AgentRuntime;

public sealed record AgentTurnRequest(
    StartedMessageProcessing Processing,
    ConnectorExecutionContext ExecutionContext);
