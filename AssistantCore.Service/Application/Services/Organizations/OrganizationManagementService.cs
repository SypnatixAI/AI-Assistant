using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Organizations;

public sealed class OrganizationManagementService(IOrganizationRepository organizationRepository)
    : IOrganizationManagementService
{
    private const int MaximumNameLength = 200;
    private const int MaximumExternalTenantIdLength = 100;

    public async Task<Organization> CreateOrganizationAsync(
        string name,
        string identityProvider,
        string externalTenantId,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = ValidateRequiredValue(name, nameof(name), MaximumNameLength);
        var normalizedExternalTenantId = ValidateRequiredValue(
            externalTenantId,
            nameof(externalTenantId),
            MaximumExternalTenantIdLength);
        var parsedIdentityProvider = ParseIdentityProvider(identityProvider);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            IdentityProvider = parsedIdentityProvider,
            ExternalTenantId = normalizedExternalTenantId,
            Status = RecordStatus.Active
        };

        return await organizationRepository.TryCreateOrganizationAsync(organization, cancellationToken)
            ?? throw new ConflictException(
                "An organization already exists for this identity provider and external tenant.");
    }

    private static IdentityProvider ParseIdentityProvider(string identityProvider)
    {
        var normalizedIdentityProvider = identityProvider?.Trim();
        var providerName = Enum.GetNames<IdentityProvider>()
            .SingleOrDefault(name => string.Equals(
                name,
                normalizedIdentityProvider,
                StringComparison.OrdinalIgnoreCase));

        if (providerName is not null)
        {
            return Enum.Parse<IdentityProvider>(providerName);
        }

        throw new BadRequestException("Identity provider must be 'MicrosoftEntraId'.");
    }

    private static string ValidateRequiredValue(string value, string propertyName, int maximumLength)
    {
        var normalizedValue = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new BadRequestException($"{propertyName} is required.");
        }

        if (normalizedValue.Length > maximumLength)
        {
            throw new BadRequestException(
                $"{propertyName} must contain at most {maximumLength} characters.");
        }

        return normalizedValue;
    }
}
