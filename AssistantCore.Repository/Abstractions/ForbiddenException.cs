namespace AssistantCore.Repository.Abstractions;

public class ForbiddenException(string message) : Exception(message)
{
}
