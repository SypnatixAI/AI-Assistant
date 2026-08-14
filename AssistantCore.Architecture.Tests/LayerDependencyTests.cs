using AssistantCore.ExternalServices;
using AssistantCore.ExternalServices.Services.OpenAI;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Services.Messages.AiModels.Providers.OpenAI;
using AssistantCore.Service.Controllers;
using AssistantCore.Service.Infrastructure.AiModels.OpenAI;
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

    [Fact]
    public void Given_Application_When_ValidateApplicationExternalDependencies_Then_ExternalServicesAndSdkAreForbidden()
    {
        // Given
        var serviceAssembly = typeof(CoreController).Assembly;

        // When
        var violations = ValidateApplicationExternalDependencies(serviceAssembly);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_ExternalServices_When_ValidateExternalServiceDependencies_Then_ServiceAndRepositoryAreForbidden()
    {
        // Given
        var externalServicesAssembly = typeof(ExternalServicesAssemblyMarker).Assembly;

        // When
        var violations = ValidateExternalServiceDependencies(externalServicesAssembly);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_ServiceTypes_When_ValidateExternalServicesAccess_Then_OnlyAdaptersAndDependencyInjectionAreAllowed()
    {
        // Given
        var serviceAssembly = typeof(CoreController).Assembly;

        // When
        var violations = ValidateExternalServicesAccess(serviceAssembly);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_ExternalServiceModels_When_ValidateExternalServiceModelDependencies_Then_SdkAndServicesAreForbidden()
    {
        // Given
        var externalServicesAssembly = typeof(ExternalServicesAssemblyMarker).Assembly;

        // When
        var violations = ValidateExternalServiceModelDependencies(externalServicesAssembly);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_ExternalSdkDependencies_When_ValidateExternalSdkAccess_Then_OnlyExternalServiceClientsAreAllowed()
    {
        // Given
        var externalServicesAssembly = typeof(ExternalServicesAssemblyMarker).Assembly;

        // When
        var violations = ValidateExternalSdkAccess(externalServicesAssembly);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_OpenAiProvider_When_ValidateOpenAiExternalCallChain_Then_ProviderUsesAdapterAndExternalClient()
    {
        // Given
        var providerType = typeof(OpenAiModelProvider);
        var applicationClientType = typeof(IOpenAiResponsesClient);
        var adapterType = typeof(OpenAiResponsesClientAdapter);
        var externalClientType = typeof(OpenAiResponsesClient);

        // When
        var violations = ValidateOpenAiExternalCallChain(
            providerType,
            applicationClientType,
            adapterType,
            externalClientType);

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

    private static IReadOnlyCollection<string> ValidateExternalServicesAccess(
        System.Reflection.Assembly serviceAssembly)
    {
        var locationResult = Types.InAssembly(serviceAssembly)
            .That()
            .HaveDependencyOn("AssistantCore.ExternalServices")
            .Should()
            .ResideInNamespaceStartingWith("AssistantCore.Service.Infrastructure")
            .GetResult();

        var roleResult = Types.InAssembly(serviceAssembly)
            .That()
            .HaveDependencyOn("AssistantCore.ExternalServices")
            .Should()
            .HaveNameEndingWith("Adapter")
            .Or()
            .HaveNameEndingWith("ServiceCollectionExtensions")
            .GetResult();

        return (locationResult.FailingTypeNames ?? [])
            .Concat(roleResult.FailingTypeNames ?? [])
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyCollection<string> ValidateApplicationExternalDependencies(
        System.Reflection.Assembly serviceAssembly)
    {
        var result = Types.InAssembly(serviceAssembly)
            .That()
            .ResideInNamespaceStartingWith("AssistantCore.Service.Application")
            .ShouldNot()
            .HaveDependencyOnAny(
                "AssistantCore.ExternalServices",
                "OpenAI.Responses",
                "System.ClientModel")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private static IReadOnlyCollection<string> ValidateExternalServiceDependencies(
        System.Reflection.Assembly externalServicesAssembly)
    {
        var result = Types.InAssembly(externalServicesAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AssistantCore.Service",
                "AssistantCore.Repository")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private static IReadOnlyCollection<string> ValidateExternalServiceModelDependencies(
        System.Reflection.Assembly externalServicesAssembly)
    {
        var result = Types.InAssembly(externalServicesAssembly)
            .That()
            .ResideInNamespaceStartingWith("AssistantCore.ExternalServices.Entities")
            .ShouldNot()
            .HaveDependencyOnAny(
                "AssistantCore.ExternalServices.Services",
                "OpenAI.Responses",
                "System.ClientModel")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private static IReadOnlyCollection<string> ValidateExternalSdkAccess(
        System.Reflection.Assembly externalServicesAssembly)
    {
        var result = Types.InAssembly(externalServicesAssembly)
            .That()
            .HaveDependencyOnAny(
                "OpenAI.Responses",
                "System.ClientModel")
            .Should()
            .ResideInNamespaceStartingWith("AssistantCore.ExternalServices.Services")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private static IReadOnlyCollection<string> ValidateOpenAiExternalCallChain(
        Type providerType,
        Type applicationClientType,
        Type adapterType,
        Type externalClientType)
    {
        var violations = new List<string>();

        if (!HasConstructorParameter(providerType, applicationClientType))
        {
            violations.Add($"{providerType.FullName} must depend on {applicationClientType.FullName}.");
        }

        if (!applicationClientType.IsAssignableFrom(adapterType))
        {
            violations.Add($"{adapterType.FullName} must implement {applicationClientType.FullName}.");
        }

        if (!HasConstructorParameter(adapterType, externalClientType))
        {
            violations.Add($"{adapterType.FullName} must depend on {externalClientType.FullName}.");
        }

        return violations;
    }

    private static bool HasConstructorParameter(Type type, Type parameterType) =>
        type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == parameterType);
}
