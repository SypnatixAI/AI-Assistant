using System.Security.Claims;
using AssistantCore.Service.Infrastructure.Authentication.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class RequiredScopeAuthorizationHandlerTests
{
    private const string RequiredScope = "access_as_user";
    private const string ScopeClaimType = "scp";
    private const string MappedScopeClaimType =
        "http://schemas.microsoft.com/identity/claims/scope";

    [Fact]
    public async Task Given_ATokenCarryingOnlyTheRequiredScope_When_HandleAsync_Then_AuthorizationSucceeds()
    {
        // Given
        var context = CreateContext(new Claim(ScopeClaimType, RequiredScope));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Given_ATokenCarryingSeveralScopesIncludingTheRequiredOne_When_HandleAsync_Then_AuthorizationSucceeds()
    {
        // Given
        var context = CreateContext(
            new Claim(ScopeClaimType, $"profile {RequiredScope} openid"));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Given_ATokenUsingTheMappedScopeClaimType_When_HandleAsync_Then_AuthorizationSucceeds()
    {
        // Given
        var context = CreateContext(new Claim(MappedScopeClaimType, RequiredScope));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Given_ATokenCarryingOnlyOtherScopes_When_HandleAsync_Then_AuthorizationFails()
    {
        // Given
        var context = CreateContext(new Claim(ScopeClaimType, "profile openid"));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Given_AnApplicationTokenWithoutAnyScopeClaim_When_HandleAsync_Then_AuthorizationFails()
    {
        // Given
        var context = CreateContext(new Claim("roles", "AssistantCore.Access"));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData("Access_As_User")]
    [InlineData("ACCESS_AS_USER")]
    public async Task Given_ATokenCarryingTheScopeInAnotherCase_When_HandleAsync_Then_AuthorizationFails(
        string scope)
    {
        // Given
        var context = CreateContext(new Claim(ScopeClaimType, scope));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Given_ATokenCarryingAnEmptyScopeClaim_When_HandleAsync_Then_AuthorizationFails(
        string scope)
    {
        // Given
        var context = CreateContext(new Claim(ScopeClaimType, scope));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Given_ATokenCarryingAScopePrefixedByTheRequiredOne_When_HandleAsync_Then_AuthorizationFails()
    {
        // Given
        var context = CreateContext(new Claim(ScopeClaimType, $"{RequiredScope}.readonly"));

        // When
        await new RequiredScopeAuthorizationHandler().HandleAsync(context);

        // Then
        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(params Claim[] claims)
    {
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, "TestAuthentication"));

        return new AuthorizationHandlerContext(
            [new RequiredScopeRequirement(RequiredScope)],
            principal,
            resource: null);
    }
}
