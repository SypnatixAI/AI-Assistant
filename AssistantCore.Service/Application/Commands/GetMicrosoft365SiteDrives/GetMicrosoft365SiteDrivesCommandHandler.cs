using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365SiteDrives;

public sealed class GetMicrosoft365SiteDrivesCommandHandler(IMicrosoft365DriveAdministrationService service)
    : IRequestHandler<GetMicrosoft365SiteDrivesCommand, IReadOnlyCollection<Microsoft365DriveResponse>>
{
    public Task<IReadOnlyCollection<Microsoft365DriveResponse>> HandleAsync(
        GetMicrosoft365SiteDrivesCommand request,
        CancellationToken cancellationToken = default) =>
        service.GetDrivesAsync(request.SiteId, cancellationToken);
}
