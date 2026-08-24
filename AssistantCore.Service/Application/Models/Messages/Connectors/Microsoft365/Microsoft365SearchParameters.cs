namespace AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

public sealed record Microsoft365SearchParameters(
    string Query,
    IReadOnlyCollection<string>? SourceTypes,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    Microsoft365SearchSecurityContext SecurityContext,
    int MaximumResults);
