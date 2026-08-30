using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMicrosoft365Sites.Models;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365Sites;

public sealed class GetMicrosoft365SitesCommandHandler(IMicrosoft365SiteDiscoveryService service)
    : IRequestHandler<GetMicrosoft365SitesCommand, GetMicrosoft365SitesResponse>
{
    public async Task<GetMicrosoft365SitesResponse> HandleAsync(
        GetMicrosoft365SitesCommand request,
        CancellationToken cancellationToken = default) =>
        new(await service.GetAvailableSitesAsync(cancellationToken));
}
