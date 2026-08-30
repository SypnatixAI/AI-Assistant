using AssistantCore.Service.Application.Configuration;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Middleware;

public sealed class Microsoft365ConsentCallbackRedirectMiddleware(
    RequestDelegate next,
    ILogger<Microsoft365ConsentCallbackRedirectMiddleware> logger,
    IOptions<Microsoft365Options> options)
{
    private static readonly PathString ConsentCallbackPath =
        new("/api/microsoft365/consent/callback");

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path != ConsentCallbackPath)
        {
            await next(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            logger.LogWarning(exception, "Microsoft 365 consent callback failed.");
            context.Response.Redirect(options.Value.ConsentErrorRedirectUrl);
        }
    }
}
