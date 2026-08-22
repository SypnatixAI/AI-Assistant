namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365SiteSourcesDiscoveryResult(
    Microsoft365SiteSourcesDiscoveryStatus Status,
    Microsoft365DiscoveredSiteSources Sources,
    TimeSpan? RetryAfterDelay = null,
    DateTimeOffset? RetryAfterAt = null)
{
    private static readonly Microsoft365DiscoveredSiteSources EmptySources = new([], []);

    public static Microsoft365SiteSourcesDiscoveryResult Succeeded(
        Microsoft365DiscoveredSiteSources sources) =>
        new(Microsoft365SiteSourcesDiscoveryStatus.Succeeded, sources);

    public static Microsoft365SiteSourcesDiscoveryResult Forbidden() =>
        new(Microsoft365SiteSourcesDiscoveryStatus.Forbidden, EmptySources);

    public static Microsoft365SiteSourcesDiscoveryResult SiteNotFound() =>
        new(Microsoft365SiteSourcesDiscoveryStatus.SiteNotFound, EmptySources);

    public static Microsoft365SiteSourcesDiscoveryResult Throttled(
        TimeSpan? retryAfterDelay,
        DateTimeOffset? retryAfterAt) =>
        new(
            Microsoft365SiteSourcesDiscoveryStatus.Throttled,
            EmptySources,
            retryAfterDelay,
            retryAfterAt);
}
