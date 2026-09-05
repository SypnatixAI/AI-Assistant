namespace AssistantCore.Service.Application.Exceptions;

public class BadRequestException(string message) : Exception(message)
{
}
