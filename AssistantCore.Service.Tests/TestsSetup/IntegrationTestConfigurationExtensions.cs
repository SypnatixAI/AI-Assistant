using Microsoft.Extensions.Configuration;

namespace AssistantCore.Service.Tests;

internal static class IntegrationTestConfigurationExtensions
{
    public static IConfigurationBuilder AddIntegrationTestDefaults(
        this IConfigurationBuilder configuration) =>
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiModels:Providers:OpenAI:ApiKey"] = "integration-test-secret",
            ["Microsoft365:ClientSecret"] = "integration-test-secret",
            ["Microsoft365:ConsentCallbackUrl"] =
                "https://localhost:7292/api/microsoft365/consent/callback",
            ["Microsoft365:ConsentSuccessRedirectUrl"] =
                "https://localhost:4200/microsoft365/consent/success",
            ["Microsoft365:ConsentErrorRedirectUrl"] =
                "https://localhost:4200/microsoft365/consent/error",
            ["Microsoft365:WebhookBaseUrl"] = "https://localhost:7292"
        });
}
