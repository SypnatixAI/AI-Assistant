namespace AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

public sealed record SearchMicrosoft365ToolArguments(
    string Query,
    IReadOnlyCollection<string>? SourceTypes,
    DateOnly? DateFrom,
    DateOnly? DateTo);
