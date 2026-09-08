using System.Net;
using System.Net.Http.Json;
using static AssistantCore.Service.Tests.AuthorizationIntegrationTestFactory;

namespace AssistantCore.Service.Tests.Integration;

/// <summary>
/// Verifie que l'endpoint interne de politique de quota exige le role applicatif dedie,
/// et refuse un token utilisateur portant le scope delegue meme accompagne d'un role
/// d'admission ou tenantAdmin.
/// </summary>
public sealed class UsagePolicyManagementAuthorizationTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly string UsagePolicyRoute = $"/api/internal/organizations/{OrganizationId}/usage-policy";

    [Theory, InlineAutoDomainData(RequiredAdmissionRole), InlineAutoDomainData(TenantAdminRole)]
    public async Task Given_AUserTokenWithADelegatedScope_When_CallingSetUsagePolicy_Then_ReturnsForbidden(
        string role)
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(ScopeHeaderName, RequiredScope);
        client.DefaultRequestHeaders.Add(RoleHeaderName, role);

        // When
        using var response = await client.PutAsJsonAsync(
            UsagePolicyRoute,
            new { monthlyTokenLimit = 1_000_000, status = "Active", effectiveAt = "2026-09-01T00:00:00Z" });

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Given_NoRole_When_CallingSetUsagePolicy_Then_ReturnsForbidden()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);

        // When
        using var response = await client.PutAsJsonAsync(
            UsagePolicyRoute,
            new { monthlyTokenLimit = 1_000_000, status = "Active", effectiveAt = "2026-09-01T00:00:00Z" });

        // Then
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Given_AnAppOnlyTokenWithTheDedicatedRole_When_CallingSetUsagePolicy_Then_AuthorizationIsGranted()
    {
        // Given
        await using var factory = CreateFactory(useTestAuthentication: true);
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(RoleHeaderName, UsagePolicyManagementRole);

        // When
        using var response = await client.PutAsJsonAsync(
            UsagePolicyRoute,
            new { monthlyTokenLimit = 1_000_000, status = "Active", effectiveAt = "2026-09-01T00:00:00Z" });
        var body = await response.Content.ReadAsStringAsync();

        // Then
        // Aucun scope delegue n'est envoye : la policy exige uniquement le role applicatif
        // dedie. La requete franchit donc l'autorisation et echoue ensuite a la resolution
        // d'identite, faute de claim de fournisseur porte par le principal de test.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("fournisseur", body, StringComparison.Ordinal);
    }
}
