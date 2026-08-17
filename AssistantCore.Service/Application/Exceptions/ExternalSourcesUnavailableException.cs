namespace AssistantCore.Service.Application.Exceptions;

public sealed class ExternalSourcesUnavailableException : Exception
{
    public const string TechnicalCode = "EXTERNAL_SOURCES_UNAVAILABLE";

    public ExternalSourcesUnavailableException()
        : base("The required external sources are currently unavailable.")
    {
    }
}
