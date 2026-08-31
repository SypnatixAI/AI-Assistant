using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Services.TenantAdmission;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class TenantAdmissionPolicyTests
{
    [Fact]
    public void Given_AnIncompleteSetupAndAStandardUser_When_Evaluate_Then_ReturnsTenantAdminRequired()
    {
        // Given
        var policy = new TenantAdmissionPolicy();

        // When
        var result = policy.Evaluate(OrganizationRole.User, isOnboardingComplete: false);

        // Then
        Assert.Equal(TenantAdmissionResult.TenantAdminRequired, result);
    }

    [Fact]
    public void Given_AnIncompleteSetupAndAnAdmin_When_Evaluate_Then_ReturnsAllowed()
    {
        // Given
        var policy = new TenantAdmissionPolicy();

        // When
        var result = policy.Evaluate(OrganizationRole.Admin, isOnboardingComplete: false);

        // Then
        Assert.Equal(TenantAdmissionResult.Allowed, result);
    }

    [Fact]
    public void Given_ACompleteSetupAndAStandardUser_When_Evaluate_Then_ReturnsAllowed()
    {
        // Given
        var policy = new TenantAdmissionPolicy();

        // When
        var result = policy.Evaluate(OrganizationRole.User, isOnboardingComplete: true);

        // Then
        Assert.Equal(TenantAdmissionResult.Allowed, result);
    }

    [Fact]
    public void Given_ACompleteSetupAndAnAdmin_When_Evaluate_Then_ReturnsAllowed()
    {
        // Given
        var policy = new TenantAdmissionPolicy();

        // When
        var result = policy.Evaluate(OrganizationRole.Admin, isOnboardingComplete: true);

        // Then
        Assert.Equal(TenantAdmissionResult.Allowed, result);
    }
}
