using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Controllers;
using NetArchTest.Rules;
using Xunit;

namespace AssistantCore.Architecture.Tests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Given_ApplicationServices_When_ValidateApplicationServiceDependencies_Then_ControllersAreForbidden()
    {
        // Given
        var serviceAssembly = typeof(CoreController).Assembly;

        // When
        var violations = ValidateApplicationServiceDependencies(serviceAssembly);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_RepositoryDomain_When_ValidateRepositoryDomainDependencies_Then_InfrastructureFrameworksAreForbidden()
    {
        // Given
        var repositoryAssembly = typeof(OrganizationMember).Assembly;

        // When
        var violations = ValidateRepositoryDomainDependencies(repositoryAssembly);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_Application_When_ValidateApplicationAuthenticationDependencies_Then_InfrastructureAndMicrosoftIdentityAreForbidden()
    {
        // Given
        var serviceAssembly = typeof(CoreController).Assembly;

        // When
        var violations = ValidateApplicationAuthenticationDependencies(serviceAssembly);

        // Then
        Assert.Empty(violations);
    }

    private static IReadOnlyCollection<string> ValidateApplicationServiceDependencies(
        System.Reflection.Assembly serviceAssembly)
    {
        var result = Types.InAssembly(serviceAssembly)
            .That()
            .ResideInNamespaceStartingWith("AssistantCore.Service.Application.Services")
            .ShouldNot()
            .HaveDependencyOn("AssistantCore.Service.Controllers")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private static IReadOnlyCollection<string> ValidateRepositoryDomainDependencies(
        System.Reflection.Assembly repositoryAssembly)
    {
        var result = Types.InAssembly(repositoryAssembly)
            .That()
            .ResideInNamespaceStartingWith("AssistantCore.Repository.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "AssistantCore.Repository.Database",
                "AssistantCore.Repository.Persistence",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private static IReadOnlyCollection<string> ValidateApplicationAuthenticationDependencies(
        System.Reflection.Assembly serviceAssembly)
    {
        var result = Types.InAssembly(serviceAssembly)
            .That()
            .ResideInNamespaceStartingWith("AssistantCore.Service.Application")
            .ShouldNot()
            .HaveDependencyOnAny(
                "AssistantCore.Service.Infrastructure.Authentication",
                "Microsoft.Identity.Web")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }
}
