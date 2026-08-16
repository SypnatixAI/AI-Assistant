namespace AssistantCore.Service.Application.Models.Messages.Connectors;

public sealed record ConnectorResult(
    IReadOnlyCollection<RetrievedEvidence> Evidence);
