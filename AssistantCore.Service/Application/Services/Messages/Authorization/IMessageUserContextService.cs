using AssistantCore.Service.Application.Models.Messages;

namespace AssistantCore.Service.Application.Services.Messages.Authorization;

public interface IMessageUserContextService
{
    Task<MessageUserContext> GetCurrentAsync(CancellationToken cancellationToken);
}
