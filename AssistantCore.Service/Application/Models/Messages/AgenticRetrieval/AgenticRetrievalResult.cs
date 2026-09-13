namespace AssistantCore.Service.Application.Models.Messages.AgenticRetrieval;

public sealed record AgenticRetrievalResult(
    string MergedContent,
    IReadOnlyCollection<AgenticRetrievalReference> References,
    IReadOnlyCollection<AgenticRetrievalActivity> Activity);
