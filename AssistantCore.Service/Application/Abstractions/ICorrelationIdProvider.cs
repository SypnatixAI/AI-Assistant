namespace AssistantCore.Service.Application.Abstractions;

public interface ICorrelationIdProvider
{
    string GetCorrelationId();
}
