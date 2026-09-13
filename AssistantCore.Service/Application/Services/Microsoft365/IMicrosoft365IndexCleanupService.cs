namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365IndexCleanupService
{
    Task CleanupAsync(
        Guid organizationId,
        Guid sourceId,
        Guid synchronizationId,
        CancellationToken cancellationToken = default);
}
