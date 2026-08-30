using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Tests.Middleware;

public sealed class Microsoft365ConsentCallbackRedirectMiddlewareTests
{
    [Theory, AutoDomainData]
    public async Task Given_AConsentCallbackFailure_When_InvokeAsync_Then_BrowserIsRedirectedToFrontend(
        string errorMessage)
    {
        // Given
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/microsoft365/consent/callback";
        var middleware = new Microsoft365ConsentCallbackRedirectMiddleware(
            _ => throw new InvalidOperationException(errorMessage),
            NullLogger<Microsoft365ConsentCallbackRedirectMiddleware>.Instance,
            Options.Create(new Microsoft365Options
            {
                ConsentErrorRedirectUrl =
                    "https://app.onpremia.example/microsoft365/consent/error"
            }));

        // When
        await middleware.InvokeAsync(context);

        // Then
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal(
            "https://app.onpremia.example/microsoft365/consent/error",
            context.Response.Headers.Location);
    }
}
