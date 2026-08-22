using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.EnableMicrosoft365List;

public sealed class EnableMicrosoft365ListCommandHandler(
    IMicrosoft365ListActivationService listActivationService)
    : IRequestHandler<EnableMicrosoft365ListCommand, Microsoft365ListResponse>
{
    public async Task<Microsoft365ListResponse> HandleAsync(
        EnableMicrosoft365ListCommand request,
        CancellationToken cancellationToken)
    {
        var list = await listActivationService.SetIndexingAsync(
            request.SiteId,
            request.ListId,
            request.IsIndexed,
            cancellationToken);

        return Microsoft365ListResponse.FromList(list);
    }
}
