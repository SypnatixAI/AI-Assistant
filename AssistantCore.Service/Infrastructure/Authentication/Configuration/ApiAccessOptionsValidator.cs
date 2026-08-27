using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Authentication.Configuration;

public sealed class ApiAccessOptionsValidator : IValidateOptions<ApiAccessOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiAccessOptions options)
    {
        var scopeError = ValidateSingleValue(
            options.RequiredScope,
            nameof(ApiAccessOptions.RequiredScope));

        if (scopeError is not null)
        {
            return ValidateOptionsResult.Fail(scopeError);
        }

        var roleError = ValidateSingleValue(
            options.RequiredAdmissionRole,
            nameof(ApiAccessOptions.RequiredAdmissionRole));

        if (roleError is not null)
        {
            return ValidateOptionsResult.Fail(roleError);
        }

        return ValidateOptionsResult.Success;
    }

    private static string? ValidateSingleValue(string value, string propertyName)
    {
        var key = $"{ApiAccessOptions.SectionName}:{propertyName}";

        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{key} is required.";
        }

        if (value.Any(char.IsWhiteSpace))
        {
            return $"{key} must be a single value without whitespace.";
        }

        return null;
    }
}
