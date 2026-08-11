using System.Security.Claims;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class HttpCurrentIdentityTests
{
    [Fact]
    public void Given_OneMatchingMapper_When_GetIdentity_Then_ReturnsMappedIdentity()
    {
        // Given
        var expectedIdentity = CreateIdentity();
        var currentIdentity = CreateCurrentIdentity(
            new StubIdentityClaimsMapper(canMap: true, expectedIdentity));

        // When
        var identity = currentIdentity.GetIdentity();

        // Then
        Assert.Same(expectedIdentity, identity);
    }

    [Fact]
    public void Given_NoMatchingMapper_When_GetIdentity_Then_ThrowsUnauthorizedAccessException()
    {
        // Given
        var currentIdentity = CreateCurrentIdentity(
            new StubIdentityClaimsMapper(canMap: false, CreateIdentity()));

        // When
        var exception = Assert.Throws<UnauthorizedAccessException>(
            currentIdentity.GetIdentity);

        // Then
        Assert.Contains("non supporte", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_MultipleMatchingMappers_When_GetIdentity_Then_ThrowsUnauthorizedAccessException()
    {
        // Given
        var currentIdentity = CreateCurrentIdentity(
            new StubIdentityClaimsMapper(canMap: true, CreateIdentity()),
            new StubIdentityClaimsMapper(canMap: true, CreateIdentity()));

        // When
        var exception = Assert.Throws<UnauthorizedAccessException>(
            currentIdentity.GetIdentity);

        // Then
        Assert.Contains("ambigu", exception.Message, StringComparison.Ordinal);
    }

    private static HttpCurrentIdentity CreateCurrentIdentity(
        params IIdentityClaimsMapper[] mappers)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([], "TestAuthentication"))
        };

        return new HttpCurrentIdentity(
            new HttpContextAccessor { HttpContext = context },
            mappers);
    }

    private static AuthenticatedIdentity CreateIdentity() => new(
        IdentityProvider.MicrosoftEntraId,
        "tenant-id",
        "user-id",
        "Test User",
        "test.user@example.com");

    private sealed class StubIdentityClaimsMapper(
        bool canMap,
        AuthenticatedIdentity identity) : IIdentityClaimsMapper
    {
        public bool CanMap(ClaimsPrincipal principal) => canMap;

        public AuthenticatedIdentity Map(ClaimsPrincipal principal) => identity;
    }
}
