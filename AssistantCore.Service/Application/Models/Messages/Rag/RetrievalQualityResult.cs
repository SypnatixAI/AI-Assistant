namespace AssistantCore.Service.Application.Models.Messages.Rag;

public sealed record RetrievalQualityResult(bool IsSufficient, double Confidence, string? Reason);
