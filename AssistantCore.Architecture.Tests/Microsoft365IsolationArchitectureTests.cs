using System.Reflection;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Repositories;
using Xunit;

namespace AssistantCore.Architecture.Tests;

/// <summary>
/// Empeche l'apparition d'une lecture ou d'une ecriture Microsoft 365 non scellee
/// sur la surface atteignable depuis une requete HTTP.
///
/// Les identifiants SharePoint sont choisis par Microsoft : deux organisations
/// peuvent detenir un site, une liste ou un lecteur portant le meme identifiant.
/// Une methode qui recoit un tel identifiant sans perimetre d'organisation
/// laisserait un appelant atteindre les donnees d'une autre organisation.
///
/// Le perimetre est acceptable sous deux formes : un parametre nomme
/// <c>organizationId</c>, ou une entite qui porte deja son organisation, comme
/// <see cref="Microsoft365Connection"/> ou une source. La seconde forme est
/// preferable : on ne peut pas se tromper d'organisation en passant l'entite
/// elle-meme.
///
/// Les repositories du Worker ne sont volontairement pas couverts : leurs
/// identifiants proviennent d'une ligne deja reclamee en base, jamais d'un
/// appelant, et leurs ecritures sont protegees par un bail.
/// </summary>
public sealed class Microsoft365IsolationArchitectureTests
{
    private const string OrganizationScopeParameterName = "organizationId";

    private static readonly Type[] ScopedEntityTypes =
    [
        typeof(Microsoft365Connection),
        typeof(Microsoft365Source)
    ];

    [Fact]
    public void Given_TheSourceDiscoveryRepository_When_ValidateMethods_Then_EachOneCarriesAnOrganizationScope()
    {
        // Given
        var repositoryInterface = typeof(IMicrosoft365SourceDiscoveryRepository);

        // When
        var inspected = GetInspectedMethods(repositoryInterface);
        var violations = FindMethodsWithoutOrganizationScope(repositoryInterface);

        // Then
        Assert.NotEmpty(inspected);
        Assert.Empty(violations);
    }

    [Fact]
    public void Given_TheConnectionRepository_When_ValidateOrganizationLookups_Then_TheyCarryAnOrganizationScope()
    {
        // Given
        var repositoryInterface = typeof(IMicrosoft365ConnectionRepository);

        // When
        var inspected = GetInspectedMethods(repositoryInterface)
            .Where(method => method.Name.Contains("ByOrganization", StringComparison.Ordinal))
            .ToArray();
        var violations = FindMethodsWithoutOrganizationScope(
            repositoryInterface,
            method => method.Name.Contains("ByOrganization", StringComparison.Ordinal));

        // Then
        Assert.NotEmpty(inspected);
        Assert.Empty(violations);
    }

    /// <summary>
    /// Expose les methodes reellement inspectees. Sans cette verification, une
    /// reflexion qui ne trouverait plus rien ferait passer la regle a vide.
    /// </summary>
    private static IReadOnlyCollection<MethodInfo> GetInspectedMethods(Type repositoryInterface) =>
        repositoryInterface
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static IReadOnlyCollection<string> FindMethodsWithoutOrganizationScope(
        Type repositoryInterface,
        Func<MethodInfo, bool>? filter = null) =>
        repositoryInterface
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => filter is null || filter(method))
            .Where(method => !HasOrganizationScope(method))
            .Select(method => $"{repositoryInterface.Name}.{method.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static bool HasOrganizationScope(MethodInfo method) =>
        method.GetParameters().Any(IsOrganizationScope);

    private static bool IsOrganizationScope(ParameterInfo parameter) =>
        (parameter.ParameterType == typeof(Guid)
            && string.Equals(parameter.Name, OrganizationScopeParameterName, StringComparison.Ordinal))
        || ScopedEntityTypes.Any(scoped => scoped.IsAssignableFrom(parameter.ParameterType));
}
