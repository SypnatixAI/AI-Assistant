using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.AuthenticateUser;
using NetArchTest.Rules;
using Xunit;

namespace AssistantCore.Architecture.Tests;

public sealed class HandlerArchitectureTests
{
    private const string CommandsNamespace = "AssistantCore.Service.Application.Commands";
    private const string ApplicationServicesNamespace = "AssistantCore.Service.Application.Services";

    [Fact]
    public void Given_Commands_When_ValidateCommandHandlerRegistrations_Then_EachCommandHasExactlyOneHandler()
    {
        // Given
        var applicationTypes = typeof(AuthenticateUserCommand).Assembly.GetTypes();

        // When
        var violations = ValidateCommandHandlerRegistrations(applicationTypes);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_CommandHandlers_When_ValidateHandlerConstructors_Then_OnlyApplicationServicesAreInjected()
    {
        // Given
        var applicationTypes = typeof(AuthenticateUserCommand).Assembly.GetTypes();

        // When
        var violations = ValidateHandlerConstructors(applicationTypes);

        // Then
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_CommandHandlers_When_ValidateHandlerDependencies_Then_ControllersAndPersistenceAreForbidden()
    {
        // Given
        var serviceAssembly = typeof(AuthenticateUserCommand).Assembly;

        // When
        var violations = ValidateHandlerDependencies(serviceAssembly);

        // Then
        Assert.Empty(violations);
    }

    private static IReadOnlyCollection<string> ValidateCommandHandlerRegistrations(IEnumerable<Type> applicationTypes)
    {
        var types = applicationTypes.ToArray();
        var commands = types
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.Namespace?.StartsWith(CommandsNamespace, StringComparison.Ordinal) == true)
            .Where(type => type.GetInterfaces().Any(IsRequestInterface))
            .ToArray();

        var handledRequestTypes = types
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(IsRequestHandlerInterface)
            .Select(handlerInterface => handlerInterface.GetGenericArguments()[0])
            .ToArray();

        return commands
            .Where(command => handledRequestTypes.Count(requestType => requestType == command) != 1)
            .Select(command => command.FullName ?? command.Name)
            .ToArray();
    }

    private static IReadOnlyCollection<string> ValidateHandlerConstructors(IEnumerable<Type> applicationTypes)
    {
        return applicationTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type.GetInterfaces().Any(IsRequestHandlerInterface))
            .Where(handler =>
            {
                var constructors = handler.GetConstructors();
                if (constructors.Length != 1)
                {
                    return true;
                }

                var parameters = constructors[0].GetParameters();
                return parameters.Length == 0 || parameters.Any(parameter =>
                    !parameter.ParameterType.IsInterface ||
                    parameter.ParameterType.Namespace?.StartsWith(
                        ApplicationServicesNamespace,
                        StringComparison.Ordinal) != true);
            })
            .Select(handler => handler.FullName ?? handler.Name)
            .ToArray();
    }

    private static IReadOnlyCollection<string> ValidateHandlerDependencies(System.Reflection.Assembly serviceAssembly)
    {
        var result = Types.InAssembly(serviceAssembly)
            .That()
            .ResideInNamespaceStartingWith(CommandsNamespace)
            .And()
            .HaveNameEndingWith("CommandHandler")
            .ShouldNot()
            .HaveDependencyOnAny(
                "AssistantCore.Service.Controllers",
                "AssistantCore.Repository.Abstractions",
                "AssistantCore.Repository.Database",
                "AssistantCore.Repository.Persistence",
                "AssistantCore.Repository.Queries",
                "AssistantCore.Service.Persistence",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        return result.FailingTypeNames ?? [];
    }

    private static bool IsRequestInterface(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>);
    }

    private static bool IsRequestHandlerInterface(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
    }
}
