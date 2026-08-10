namespace AssistantCore.Service.Application.Models.Messages.Lifecycle;

public sealed record MessageProcessingFailure(
    string ErrorCode,
    bool WasCancelled);
