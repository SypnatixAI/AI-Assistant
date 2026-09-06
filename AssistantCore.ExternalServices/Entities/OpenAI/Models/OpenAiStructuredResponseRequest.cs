namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiStructuredResponseRequest(
    string Model,
    string Instructions,
    string UserMessage,
    string SchemaName,
    string SchemaJson,
    string SchemaDescription);
