using System.Net;
using static AssistantCore.Service.Tests.AuthorizationIntegrationTestFactory;

namespace AssistantCore.Service.Tests.Integration;

/// <summary>
/// Verifie qu'un role Entra autorisant l'admission est exige sur les endpoints
/// utilisateur, en plus du scope delegue.
/// </summary>
public sealed class RequiredAppRoleAuthorizationTests
{
    [Fact]
    public async Task Given_AnAuthenticatedTokenWithoutAnyRole_When_CallingAuthenticateUser_Then_ReturnsForbidden()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(ScopeHeaderName, RequiredScope);

        // When
        using var response = await client.GetAsync(AuthenticateUserRoute);

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Given_AnAuthenticatedTokenWithAnotherRole_When_CallingAuthenticateUser_Then_ReturnsForbidden()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(ScopeHeaderName, RequiredScope);
        client.DefaultRequestHeaders.Add(RoleHeaderName, "SomeOtherApp.Access");

        // When
        using var response = await client.GetAsync(AuthenticateUserRoute);

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory, InlineAutoDomainData(RequiredAdmissionRole), InlineAutoDomainData(TenantAdminRole)]
    public async Task Given_AnAuthenticatedTokenWithAnAcceptedRole_When_CallingAuthenticateUser_Then_AuthorizationIsGranted(
        string role)
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(ScopeHeaderName, RequiredScope);
        client.DefaultRequestHeaders.Add(RoleHeaderName, role);

        // When
        using var response = await client.GetAsync(AuthenticateUserRoute);
        var body = await response.Content.ReadAsStringAsync();

        // Then
        // Le scope et le role passent : la requete atteint la lecture d'identite, qui
        // refuse ensuite le principal de test parce qu'il ne porte aucun claim de fournisseur.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("fournisseur", body, StringComparison.Ordinal);
    }
}
