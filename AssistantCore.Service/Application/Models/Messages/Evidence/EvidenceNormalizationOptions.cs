namespace AssistantCore.Service.Application.Models.Messages.Evidence;

public sealed record EvidenceNormalizationOptions(
    int MaximumContentLength,
    int MaximumResults);
