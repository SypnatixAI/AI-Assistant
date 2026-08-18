using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Authentication.Configuration;

public sealed class ApiAccessOptionsValidator : IValidateOptions<ApiAccessOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiAccessOptions options)
    {
        var key = $"{ApiAccessOptions.SectionName}:{nameof(ApiAccessOptions.RequiredScope)}";

        if (string.IsNullOrWhiteSpace(options.RequiredScope))
        {
            return ValidateOptionsResult.Fail($"{key} is required.");
        }

        if (options.RequiredScope.Any(char.IsWhiteSpace))
        {
            return ValidateOptionsResult.Fail(
                $"{key} must be a single value without whitespace.");
        }

        return ValidateOptionsResult.Success;
    }
}
