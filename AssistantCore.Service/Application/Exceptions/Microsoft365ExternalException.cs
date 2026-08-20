namespace AssistantCore.Service.Application.Exceptions;

public sealed class Microsoft365ExternalException(string message, Exception? innerException = null)
    : Exception(message, innerException);
