namespace AssistantCore.ExternalServices.Entities.Azure;

public sealed record AzureOpenAiModelConfiguration(
    string ResourceUri,
    string DeploymentId,
    string ModelName,
    string ApiKey);
