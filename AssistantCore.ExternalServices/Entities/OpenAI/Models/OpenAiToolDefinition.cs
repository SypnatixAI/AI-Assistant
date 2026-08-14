namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiToolDefinition(
    string Name,
    string Description,
    string InputSchemaJson);
