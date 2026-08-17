using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class AiToolFailureWarningFactory : IAiToolFailureWarningFactory
{
    public string Create(string toolName) => toolName switch
    {
        AiToolNames.SearchMicrosoft365 => "Microsoft 365 could not be consulted.",
        AiToolNames.QueryErp => "The ERP could not be consulted.",
        AiToolNames.QueryCrm => "The CRM could not be consulted.",
        AiToolNames.SearchInternalData => "Internal data could not be consulted.",
        _ => "A requested source could not be consulted."
    };
}
