using AssistantCore.Service.Application.Models.Authentication;

namespace AssistantCore.Service.Application.Abstractions;

public interface ICurrentIdentity
{
    AuthenticatedIdentity GetIdentity();
}
