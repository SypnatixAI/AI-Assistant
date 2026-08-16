namespace AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

public sealed record QueryCrmToolArguments(
    string Query,
    IReadOnlyCollection<string>? EntityTypes);
