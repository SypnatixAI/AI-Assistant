using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.AiModels.Configuration;

public sealed class AiModelsOptionsValidator : IValidateOptions<AiModelsOptions>
{
    public ValidateOptionsResult Validate(string? name, AiModelsOptions options)
    {
        var errors = new List<string>();
        var activeModels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            errors.Add("AiModels:DefaultModel is required.");
        }

        foreach (var (providerName, provider) in options.Providers)
        {
            ValidateProvider(providerName, provider, activeModels, errors);
        }

        if (activeModels.Count is 0)
        {
            errors.Add("At least one model must be active for an enabled provider.");
        }
        else if (!activeModels.ContainsKey(options.DefaultModel))
        {
            errors.Add("AiModels:DefaultModel must reference an active configured model.");
        }

        return errors.Count is 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateProvider(
        string providerName,
        AiModelProviderOptions provider,
        IDictionary<string, string> activeModels,
        ICollection<string> errors)
    {
        if (!provider.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            errors.Add("An enabled AI model provider must have a name.");
        }

        if (!IsValidHttpsEndpoint(provider.Endpoint))
        {
            errors.Add($"AiModels:Providers:{providerName}:Endpoint must be a valid HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            errors.Add($"AiModels:Providers:{providerName}:ApiKey is required.");
        }

        if (provider.TimeoutSeconds <= 0)
        {
            errors.Add($"AiModels:Providers:{providerName}:TimeoutSeconds must be greater than zero.");
        }

        foreach (var (modelName, model) in provider.Models.Where(model => model.Value.Enabled))
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                errors.Add($"AiModels:Providers:{providerName}:Models contains an active model without a name.");
                continue;
            }

            if (!activeModels.TryAdd(modelName, providerName))
            {
                errors.Add($"The active model '{modelName}' is configured by more than one provider.");
            }
        }
    }

    private static bool IsValidHttpsEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}
