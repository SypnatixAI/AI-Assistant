namespace AssistantCore.Service.Application.Models.Messages.Rag;

public sealed record RagPassage(string Reference, string Content, string SourceIdentity, double? SemanticScore = null);
