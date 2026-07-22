namespace AssistantCore.Repository.Abstractions;

public sealed class ForbiddenException(string message) : Exception(message)
{
}
