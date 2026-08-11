using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Controllers;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;
using Xunit;

namespace AssistantCore.Architecture.Tests;

public sealed class ControllerArchitectureTests
{
    private const string ControllersNamespace = "AssistantCore.Service.Controllers";
    private const string ApplicationServicesNamespace = "AssistantCore.Service.Application.Services";
    private static readonly Type[] ControllerTypes = typeof(CoreController).Assembly
        .GetTypes()
        .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ControllerBase)))
        .ToArray();

    [Fact]
    public void Given_Controllers_When_ValidateControllerConstructors_Then_OnlyDispatcherIsInjected()
    {
        // Given
        var controllers = ControllerTypes;

        // When
        var violations = ValidateControllerConstructors(controllers);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_Controllers_When_ValidateControllerDependencies_Then_ApplicationServicesAndPersistenceAreForbidden()
    {
        // Given
        var serviceAssembly = typeof(CoreController).Assembly;

        // When
        var violations = ValidateControllerDependencies(serviceAssembly);

        // Then
        Assert.Empty(violations);
    }

    private static IReadOnlyCollection<string> ValidateControllerConstructors(IEnumerable<Type> controllers)
    {
        return controllers
            .Where(controller =>
            {
                var constructors = controller.GetConstructors();
                if (constructors.Length != 1)
                {
                    return true;
                }

                var parameters = constructors[0].GetParameters();
                return parameters.Length != 1 || parameters[0].ParameterType != typeof(IDispatcher);
            })
            .Select(controller => controller.FullName ?? controller.Name)
            .ToArray();
    }

    private static IReadOnlyCollection<string> ValidateControllerDependencies(System.Reflection.Assembly serviceAssembly)
    {
        var result = Types.InAssembly(serviceAssembly)
            .That()
            .ResideInNamespaceStartingWith(ControllersNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationServicesNamespace,
                "AssistantCore.Repository.Abstractions",
                "AssistantCore.Repository.Database",
                "AssistantCore.Repository.Persistence",
                "AssistantCore.Repository.Queries",
                "AssistantCore.Service.Persistence")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }
}
