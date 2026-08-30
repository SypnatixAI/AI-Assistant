using System.Security.Claims;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Infrastructure.Authentication;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class MicrosoftEntraIdentityClaimsMapperTests
{
    [Theory, AutoDomainData]
    public void Given_ValidEntraClaims_When_Map_Then_ReturnsNormalizedIdentity(
        string tenantId,
        string userId,
        string displayName,
        string email)
    {
        // Given
        var principal = CreatePrincipal(
            new Claim("iss", $"https://login.microsoftonline.com/{tenantId}/v2.0"),
            new Claim("tid", tenantId),
            new Claim("oid", userId),
            new Claim("name", displayName),
            new Claim("preferred_username", email));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var identity = mapper.Map(principal);

        // Then
        Assert.Equal(IdentityProvider.MicrosoftEntraId, identity.Provider);
        Assert.Equal(tenantId, identity.ExternalOrganizationId);
        Assert.Equal(userId, identity.ExternalUserId);
        Assert.Equal(displayName, identity.DisplayName);
        Assert.Equal(email, identity.Email);
        Assert.Empty(identity.AppRoles);
    }

    [Theory, AutoDomainData]
    public void Given_MultipleRolesClaims_When_Map_Then_ReturnsAllOfThem(
        string tenantId,
        string userId)
    {
        // Given
        var principal = CreatePrincipal(
            new Claim("tid", tenantId),
            new Claim("oid", userId),
            new Claim("roles", "AssistantCore.Access"),
            new Claim("roles", "tenantAdmin"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var identity = mapper.Map(principal);

        // Then
        Assert.Equal(["AssistantCore.Access", "tenantAdmin"], identity.AppRoles.Order(StringComparer.Ordinal));
    }

    [Theory, AutoDomainData]
    public void Given_ASpaceSeparatedRolesClaim_When_Map_Then_SplitsIntoIndividualRoles(
        string tenantId,
        string userId)
    {
        // Given
        var principal = CreatePrincipal(
            new Claim("tid", tenantId),
            new Claim("oid", userId),
            new Claim("roles", "AssistantCore.Access tenantAdmin"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var identity = mapper.Map(principal);

        // Then
        Assert.Equal(["AssistantCore.Access", "tenantAdmin"], identity.AppRoles.Order(StringComparer.Ordinal));
    }

    [Theory, AutoDomainData]
    public void Given_ANativeClaimsRoleType_When_Map_Then_ReadsItToo(
        string tenantId,
        string userId)
    {
        // Given
        var principal = CreatePrincipal(
            new Claim("tid", tenantId),
            new Claim("oid", userId),
            new Claim(ClaimTypes.Role, "tenantAdmin"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var identity = mapper.Map(principal);

        // Then
        Assert.Equal(["tenantAdmin"], identity.AppRoles);
    }

    [Theory, AutoDomainData]
    public void Given_EntraIssuer_When_CanMap_Then_ReturnsTrue(string tenantId)
    {
        // Given
        var principal = CreatePrincipal(
            new Claim("iss", $"https://login.microsoftonline.com/{tenantId}/v2.0"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var canMap = mapper.CanMap(principal);

        // Then
        Assert.True(canMap);
    }

    [Theory, AutoDomainData]
    public void Given_MappedEntraTenantClaim_When_CanMap_Then_ReturnsTrue(string tenantId)
    {
        // Given
        var principal = CreatePrincipal(
            new Claim(
                "http://schemas.microsoft.com/identity/claims/tenantid",
                tenantId));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var canMap = mapper.CanMap(principal);

        // Then
        Assert.True(canMap);
    }

    [Theory, AutoDomainData]
    public void Given_EntraTokenWithoutObjectIdentifier_When_Map_Then_ThrowsUnauthorizedAccessException(
        string tenantId)
    {
        // Given
        var principal = CreatePrincipal(new Claim("tid", tenantId));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var exception = Assert.Throws<UnauthorizedAccessException>(() => mapper.Map(principal));

        // Then
        Assert.Contains("oid", exception.Message, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuthentication"));
}
