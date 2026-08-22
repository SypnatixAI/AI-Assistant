using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365DiscoveredDrive(
    string SiteId,
    string DriveId,
    string DisplayName,
    string? WebUrl)
{
    public Microsoft365SourceStatus Status => Microsoft365SourceStatus.Discovered;

    public bool IsIndexed => false;
}
