namespace AssistantCore.Service.Application.Exceptions;

/// <summary>
/// Portee par une exception qui expose un code d'erreur stable destine au frontend.
/// Le code reste identique entre les versions afin que le client puisse distinguer
/// deux situations partageant le meme statut HTTP.
/// </summary>
public interface IErrorCodeException
{
    string ErrorCode { get; }
}
