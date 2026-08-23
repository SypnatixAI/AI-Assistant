namespace AssistantCore.Service.Application.Exceptions;

public sealed class Microsoft365GraphTransientException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
