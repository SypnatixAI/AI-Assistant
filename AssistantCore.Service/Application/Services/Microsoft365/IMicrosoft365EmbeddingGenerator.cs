namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365EmbeddingGenerator
{
    Task<IReadOnlyList<IReadOnlyList<float>>> CreateAsync(
        IReadOnlyCollection<string> contents,
        CancellationToken cancellationToken = default);
}
