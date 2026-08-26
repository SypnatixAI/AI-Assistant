using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365SiteDrives;

public sealed record GetMicrosoft365SiteDrivesCommand(string SiteId)
    : IRequest<IReadOnlyCollection<Microsoft365DriveResponse>>;
