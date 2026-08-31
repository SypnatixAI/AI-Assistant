namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365UserGroupResolver
{
    Task<IReadOnlyCollection<string>> ResolveGroupIdsAsync(
        string externalTenantId,
        string entraUserId,
        CancellationToken cancellationToken);
}
