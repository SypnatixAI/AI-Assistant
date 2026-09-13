using AssistantCore.Service.Infrastructure.Authentication.Authorization;
using AssistantCore.Service.Infrastructure.Authentication.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class ConfigureApiAuthorizationOptionsTests
{
    [Theory, InlineAutoDomainData("access_as_user", "AssistantCore.Management.Admin")]
    public void Given_ApiAccessOptions_When_Configure_Then_RegistersManagementAdminPolicy(
        string requiredScope,
        string managementAdminRole)
    {
        // Given
        var apiAccessOptions = CreateApiAccessOptions(
            requiredScope,
            managementAdminRole);
        var authorizationOptions = new AuthorizationOptions();

        // When
        new ConfigureApiAuthorizationOptions(Options.Create(apiAccessOptions))
            .Configure(authorizationOptions);

        // Then
        var policy = authorizationOptions.GetPolicy(ApiAuthorizationPolicies.ManagementAdmin);
        Assert.NotNull(policy);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is RequiredScopeRequirement scopeRequirement
                && scopeRequirement.RequiredScope == requiredScope);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is RequiredAppRoleRequirement roleRequirement
                && roleRequirement.AcceptedRoles.Contains(managementAdminRole));
    }

    [Theory, InlineAutoDomainData("AssistantCore.Management.Admin")]
    public void Given_ApiAccessOptions_When_Configure_Then_DefaultPolicyDoesNotAcceptManagementAdminRole(
        string managementAdminRole)
    {
        // Given
        var apiAccessOptions = CreateApiAccessOptions(
            "access_as_user",
            managementAdminRole);
        var authorizationOptions = new AuthorizationOptions();

        // When
        new ConfigureApiAuthorizationOptions(Options.Create(apiAccessOptions))
            .Configure(authorizationOptions);

        // Then
        var appRoleRequirement = authorizationOptions.DefaultPolicy.Requirements
            .OfType<RequiredAppRoleRequirement>()
            .Single();
        Assert.DoesNotContain(managementAdminRole, appRoleRequirement.AcceptedRoles);
    }

    [Theory, InlineAutoDomainData("josetchibozo7@hotmail.com")]
    public void Given_ManagementAdminAllowedEmail_When_Configure_Then_ManagementAdminPolicyAcceptsEmail(
        string allowedEmail)
    {
        // Given
        var apiAccessOptions = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "TenantAdmin",
            ManagementAdminRole = "AssistantCore.Management.Admin",
            ManagementAdminAllowedEmails = [allowedEmail]
        };
        var authorizationOptions = new AuthorizationOptions();

        // When
        new ConfigureApiAuthorizationOptions(Options.Create(apiAccessOptions))
            .Configure(authorizationOptions);

        // Then
        var policy = authorizationOptions.GetPolicy(ApiAuthorizationPolicies.ManagementAdmin);
        Assert.NotNull(policy);
        Assert.Contains(
            policy.Requirements,
            requirement => requirement is RequiredAppRoleRequirement roleRequirement
                && roleRequirement.AcceptedEmails.Contains(allowedEmail));
    }

    private static ApiAccessOptions CreateApiAccessOptions(
        string requiredScope,
        string managementAdminRole) =>
        new()
        {
            RequiredScope = requiredScope,
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "TenantAdmin",
            ManagementAdminRole = managementAdminRole
        };
}
