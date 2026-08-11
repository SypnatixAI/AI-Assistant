using System.Security.Claims;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Infrastructure.Authentication;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class MicrosoftEntraIdentityClaimsMapperTests
{
    [Fact]
    public void Given_ValidEntraClaims_When_Map_Then_ReturnsNormalizedIdentity()
    {
        // Given
        var principal = CreatePrincipal(
            new Claim("iss", "https://login.microsoftonline.com/tenant-id/v2.0"),
            new Claim("tid", "tenant-id"),
            new Claim("oid", "user-id"),
            new Claim("name", "Marie Tremblay"),
            new Claim("preferred_username", "marie@example.com"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var identity = mapper.Map(principal);

        // Then
        Assert.Equal(IdentityProvider.MicrosoftEntraId, identity.Provider);
        Assert.Equal("tenant-id", identity.ExternalOrganizationId);
        Assert.Equal("user-id", identity.ExternalUserId);
        Assert.Equal("Marie Tremblay", identity.DisplayName);
        Assert.Equal("marie@example.com", identity.Email);
    }

    [Fact]
    public void Given_EntraIssuer_When_CanMap_Then_ReturnsTrue()
    {
        // Given
        var principal = CreatePrincipal(
            new Claim("iss", "https://login.microsoftonline.com/tenant-id/v2.0"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var canMap = mapper.CanMap(principal);

        // Then
        Assert.True(canMap);
    }

    [Fact]
    public void Given_MappedEntraTenantClaim_When_CanMap_Then_ReturnsTrue()
    {
        // Given
        var principal = CreatePrincipal(
            new Claim(
                "http://schemas.microsoft.com/identity/claims/tenantid",
                "tenant-id"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var canMap = mapper.CanMap(principal);

        // Then
        Assert.True(canMap);
    }

    [Fact]
    public void Given_EntraTokenWithoutObjectIdentifier_When_Map_Then_ThrowsUnauthorizedAccessException()
    {
        // Given
        var principal = CreatePrincipal(new Claim("tid", "tenant-id"));
        var mapper = new MicrosoftEntraIdentityClaimsMapper();

        // When
        var exception = Assert.Throws<UnauthorizedAccessException>(() => mapper.Map(principal));

        // Then
        Assert.Contains("oid", exception.Message, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuthentication"));
}
