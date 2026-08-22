namespace AssistantCore.Service.Application.Exceptions;

public sealed class ConflictException(string message) : Exception(message)
{
}
