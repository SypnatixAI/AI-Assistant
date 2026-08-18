using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ConsentStateProtector
{
    string Protect(Microsoft365ConsentState state);

    Microsoft365ConsentState Unprotect(string protectedState);
}
