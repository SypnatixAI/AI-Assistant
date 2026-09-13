using System.Security.Claims;
using AssistantCore.Service.Infrastructure.Authentication.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AssistantCore.Service.Tests.Authentication;

public sealed class RequiredAppRoleAuthorizationHandlerTests
{
    [Theory, InlineAutoDomainData("josetchibozo7@hotmail.com")]
    public async Task Given_AllowedEmailWithoutRole_When_HandleAsync_Then_AuthorizationSucceeds(
        string allowedEmail)
    {
        // Given
        var requirement = new RequiredAppRoleRequirement(
            ["AssistantCore.Management.Admin"],
            [allowedEmail]);
        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim("preferred_username", allowedEmail)],
                "Test"));
        var context = new AuthorizationHandlerContext([requirement], user, null);
        var handler = new RequiredAppRoleAuthorizationHandler();

        // When
        await handler.HandleAsync(context);

        // Then
        Assert.True(context.HasSucceeded);
    }

    [Theory, InlineAutoDomainData("other@hotmail.com")]
    public async Task Given_NotAllowedEmailWithoutRole_When_HandleAsync_Then_AuthorizationDoesNotSucceed(
        string email)
    {
        // Given
        var requirement = new RequiredAppRoleRequirement(
            ["AssistantCore.Management.Admin"],
            ["josetchibozo7@hotmail.com"]);
        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim("preferred_username", email)],
                "Test"));
        var context = new AuthorizationHandlerContext([requirement], user, null);
        var handler = new RequiredAppRoleAuthorizationHandler();

        // When
        await handler.HandleAsync(context);

        // Then
        Assert.False(context.HasSucceeded);
    }
}
