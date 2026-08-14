namespace AssistantCore.ExternalServices.Entities.OpenAI.Models;

public sealed record OpenAiClientSettings(
    string Endpoint,
    string ApiKey);
