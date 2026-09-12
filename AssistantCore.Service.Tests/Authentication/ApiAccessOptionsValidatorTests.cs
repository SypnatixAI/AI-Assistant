using AssistantCore.Service.Infrastructure.Authentication.Configuration;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class ApiAccessOptionsValidatorTests
{
    [Fact]
    public void Given_AConfiguredScopeAndRole_When_Validate_Then_ValidationSucceeds()
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "tenantAdmin",
            ManagementAdminRole = "AssistantCore.Management.Admin"
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_AMissingScope_When_Validate_Then_ValidationFailsOnTheScopeKey(string scope)
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = scope,
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "tenantAdmin",
            ManagementAdminRole = "AssistantCore.Management.Admin"
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains("AzureAd:RequiredScope is required.", result.Failures);
    }

    [Fact]
    public void Given_AScopeContainingSeveralValues_When_Validate_Then_ValidationFails()
    {
        // Given
        // Une liste de scopes ici rendrait l'exigence impossible a satisfaire silencieusement.
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user profile",
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "tenantAdmin",
            ManagementAdminRole = "AssistantCore.Management.Admin"
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            "AzureAd:RequiredScope must be a single value without whitespace.",
            result.Failures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_AMissingAdmissionRole_When_Validate_Then_ValidationFailsOnTheRoleKey(string role)
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = role,
            TenantAdminRole = "tenantAdmin",
            ManagementAdminRole = "AssistantCore.Management.Admin"
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains("AzureAd:RequiredAdmissionRole is required.", result.Failures);
    }

    [Fact]
    public void Given_AnAdmissionRoleContainingSeveralValues_When_Validate_Then_ValidationFails()
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = "AssistantCore.Access Some.Other",
            TenantAdminRole = "tenantAdmin",
            ManagementAdminRole = "AssistantCore.Management.Admin"
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            "AzureAd:RequiredAdmissionRole must be a single value without whitespace.",
            result.Failures);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_AMissingTenantAdminRole_When_Validate_Then_ValidationFailsOnTheTenantAdminRoleKey(string role)
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = role,
            ManagementAdminRole = "AssistantCore.Management.Admin"
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains("AzureAd:TenantAdminRole is required.", result.Failures);
    }

    [Fact]
    public void Given_ATenantAdminRoleContainingSeveralValues_When_Validate_Then_ValidationFails()
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "tenantAdmin Some.Other",
            ManagementAdminRole = "AssistantCore.Management.Admin"
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            "AzureAd:TenantAdminRole must be a single value without whitespace.",
            result.Failures);
    }

    [Theory]
    [InlineAutoDomainData("")]
    [InlineAutoDomainData("   ")]
    public void Given_AMissingManagementAdminRole_When_Validate_Then_ValidationFailsOnTheManagementAdminRoleKey(
        string role)
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "tenantAdmin",
            ManagementAdminRole = role
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains("AzureAd:ManagementAdminRole is required.", result.Failures);
    }

    [Theory, InlineAutoDomainData("AssistantCore.Management.Admin Some.Other")]
    public void Given_AManagementAdminRoleContainingSeveralValues_When_Validate_Then_ValidationFails(
        string role)
    {
        // Given
        var options = new ApiAccessOptions
        {
            RequiredScope = "access_as_user",
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "tenantAdmin",
            ManagementAdminRole = role
        };

        // When
        var result = new ApiAccessOptionsValidator().Validate(name: null, options);

        // Then
        Assert.True(result.Failed);
        Assert.Contains(
            "AzureAd:ManagementAdminRole must be a single value without whitespace.",
            result.Failures);
    }
}
