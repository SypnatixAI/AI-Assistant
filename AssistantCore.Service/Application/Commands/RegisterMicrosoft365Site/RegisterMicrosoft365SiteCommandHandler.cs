using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.RegisterMicrosoft365Site;

public sealed class RegisterMicrosoft365SiteCommandHandler(IMicrosoft365DriveAdministrationService service)
    : IRequestHandler<RegisterMicrosoft365SiteCommand, Microsoft365SiteResponse>
{
    public Task<Microsoft365SiteResponse> HandleAsync(
        RegisterMicrosoft365SiteCommand request,
        CancellationToken cancellationToken = default) =>
        service.RegisterSiteAsync(request.SiteId, cancellationToken);
}
