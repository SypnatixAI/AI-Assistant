using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class OrganizationRoleResolverTests
{
    [Fact]
    public void Given_NoAppRoles_When_Resolve_Then_ThrowsForbiddenException()
    {
        // Given
        var resolver = CreateResolver();

        // When / Then
        var exception = Assert.Throws<ForbiddenException>(() => resolver.Resolve([]));
        Assert.Equal("Organization member access denied.", exception.Message);
    }

    [Fact]
    public void Given_OnlyTheAdmissionRole_When_Resolve_Then_ReturnsUser()
    {
        // Given
        var resolver = CreateResolver();

        // When
        var role = resolver.Resolve(["AssistantCore.Access"]);

        // Then
        Assert.Equal(OrganizationRole.User, role);
    }

    [Fact]
    public void Given_TheAdmissionRoleAndTenantAdmin_When_Resolve_Then_ReturnsAdmin()
    {
        // Given
        var resolver = CreateResolver();

        // When
        var role = resolver.Resolve(["AssistantCore.Access", "TenantAdmin"]);

        // Then
        Assert.Equal(OrganizationRole.Admin, role);
    }

    [Fact]
    public void Given_TenantAdminWithoutTheAdmissionRole_When_Resolve_Then_ReturnsAdmin()
    {
        // Given
        var resolver = CreateResolver();

        // When
        var role = resolver.Resolve(["TenantAdmin"]);

        // Then
        Assert.Equal(OrganizationRole.Admin, role);
    }

    [Fact]
    public void Given_ANativeMicrosoftAdministratorRole_When_Resolve_Then_DoesNotGrantAdmin()
    {
        // Given
        var resolver = CreateResolver();

        // When
        var role = resolver.Resolve(["AssistantCore.Access", "Global Administrator"]);

        // Then
        Assert.Equal(OrganizationRole.User, role);
    }

    private static OrganizationRoleResolver CreateResolver() =>
        new(Options.Create(new OrganizationRoleOptions
        {
            RequiredAdmissionRole = "AssistantCore.Access",
            TenantAdminRole = "TenantAdmin"
        }));
}
