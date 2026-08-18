using System.Security.Claims;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Models.Authentication;
using AssistantCore.Service.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class HttpCurrentIdentityTests
{
    [Theory, AutoDomainData]
    public void Given_AnUnauthenticatedPrincipal_When_GetIdentity_Then_ThrowsUnauthorizedAccessException(
        Guid _)
    {
        // Given
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        var currentIdentity = new HttpCurrentIdentity(
            new HttpContextAccessor { HttpContext = context },
            []);

        // When
        var exception = Assert.Throws<UnauthorizedAccessException>(
            currentIdentity.GetIdentity);

        // Then
        Assert.Equal("Aucun utilisateur authentifie.", exception.Message);
    }

    [Theory, AutoDomainData]
    public void Given_OneMatchingMapper_When_GetIdentity_Then_ReturnsMappedIdentity(
        AuthenticatedIdentity expectedIdentity)
    {
        // Given
        var currentIdentity = CreateCurrentIdentity(
            new StubIdentityClaimsMapper(canMap: true, expectedIdentity));

        // When
        var identity = currentIdentity.GetIdentity();

        // Then
        Assert.Same(expectedIdentity, identity);
    }

    [Theory, AutoDomainData]
    public void Given_NoMatchingMapper_When_GetIdentity_Then_ThrowsUnauthorizedAccessException(
        AuthenticatedIdentity authenticatedIdentity)
    {
        // Given
        var currentIdentity = CreateCurrentIdentity(
            new StubIdentityClaimsMapper(canMap: false, authenticatedIdentity));

        // When
        var exception = Assert.Throws<UnauthorizedAccessException>(
            currentIdentity.GetIdentity);

        // Then
        Assert.Contains("non supporte", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public void Given_MultipleMatchingMappers_When_GetIdentity_Then_ThrowsUnauthorizedAccessException(
        AuthenticatedIdentity firstIdentity,
        AuthenticatedIdentity secondIdentity)
    {
        // Given
        var currentIdentity = CreateCurrentIdentity(
            new StubIdentityClaimsMapper(canMap: true, firstIdentity),
            new StubIdentityClaimsMapper(canMap: true, secondIdentity));

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

    private sealed class StubIdentityClaimsMapper(
        bool canMap,
        AuthenticatedIdentity identity) : IIdentityClaimsMapper
    {
        public bool CanMap(ClaimsPrincipal principal) => canMap;

        public AuthenticatedIdentity Map(ClaimsPrincipal principal) => identity;
    }
}
