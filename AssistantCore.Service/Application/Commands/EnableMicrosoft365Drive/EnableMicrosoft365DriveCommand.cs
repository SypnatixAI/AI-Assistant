using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.EnableMicrosoft365Drive;

public sealed record EnableMicrosoft365DriveCommand(string SiteId, string DriveId, bool IsIndexed)
    : IRequest<Microsoft365DriveResponse>;
