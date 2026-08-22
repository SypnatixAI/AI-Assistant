using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ClientStateProtector
{
    Microsoft365ClientState Create();

    bool Matches(string clientState, string protectedClientState);
}
