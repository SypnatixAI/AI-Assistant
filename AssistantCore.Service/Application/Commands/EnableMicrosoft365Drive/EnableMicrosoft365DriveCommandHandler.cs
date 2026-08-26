using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.EnableMicrosoft365Drive;

public sealed class EnableMicrosoft365DriveCommandHandler(IMicrosoft365DriveAdministrationService service)
    : IRequestHandler<EnableMicrosoft365DriveCommand, Microsoft365DriveResponse>
{
    public Task<Microsoft365DriveResponse> HandleAsync(
        EnableMicrosoft365DriveCommand request,
        CancellationToken cancellationToken = default) =>
        service.EnableDriveAsync(request.SiteId, request.DriveId, request.IsIndexed, cancellationToken);
}
