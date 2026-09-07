namespace AssistantCore.Service.Application.Models.Messages.Rag;

public sealed record GroundednessResult(bool Passed, double Confidence, string? Reason);
