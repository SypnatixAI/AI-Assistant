namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SearchIndexInitializer
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}
