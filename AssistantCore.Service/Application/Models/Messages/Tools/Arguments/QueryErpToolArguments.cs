namespace AssistantCore.Service.Application.Models.Messages.Tools.Arguments;

public sealed record QueryErpToolArguments(
    string Metric,
    DateOnly? DateFrom,
    DateOnly? DateTo);
