using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection.Models;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.RevokeMicrosoft365Connection;

public sealed class RevokeMicrosoft365ConnectionCommandHandler(
    IMicrosoft365ConnectionService connectionService)
    : IRequestHandler<RevokeMicrosoft365ConnectionCommand, RevokeMicrosoft365ConnectionResponse>
{
    public async Task<RevokeMicrosoft365ConnectionResponse> HandleAsync(
        RevokeMicrosoft365ConnectionCommand request,
        CancellationToken cancellationToken)
    {
        var result = await connectionService.RevokeAsync(
            request.ConnectionId,
            cancellationToken);

        return new RevokeMicrosoft365ConnectionResponse(
            result.ConnectionId,
            result.Status.ToString());
    }
}
