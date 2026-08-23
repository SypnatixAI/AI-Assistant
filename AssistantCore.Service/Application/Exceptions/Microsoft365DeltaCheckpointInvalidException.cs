namespace AssistantCore.Service.Application.Exceptions;

public sealed class Microsoft365DeltaCheckpointInvalidException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
