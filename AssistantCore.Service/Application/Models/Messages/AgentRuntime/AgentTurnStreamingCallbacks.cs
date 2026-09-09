namespace AssistantCore.Service.Application.Models.Messages.AgentRuntime;

public sealed record AgentTurnStreamingCallbacks(
    Func<string, CancellationToken, ValueTask> OnProgress,
    Func<string, CancellationToken, ValueTask> OnAnswerDelta);
