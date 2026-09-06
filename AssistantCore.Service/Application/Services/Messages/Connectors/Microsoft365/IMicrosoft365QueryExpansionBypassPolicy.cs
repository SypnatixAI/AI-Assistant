namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365QueryExpansionBypassPolicy
{
    bool ShouldExpand(string query);
}
