namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365DiscoveredSiteSources(
    IReadOnlyCollection<Microsoft365DiscoveredDrive> Drives,
    IReadOnlyCollection<Microsoft365DiscoveredList> Lists);
