using System.Text.Json;
using AssistantCore.Repository.Abstractions;

namespace AssistantCore.Service.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ForbiddenException exception)
        {
            logger.LogWarning(exception, "Access denied while processing the request.");

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new ExceptionResponse(
                exception.Message,
                environment.IsDevelopment() ? exception.Message : null);

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new ExceptionResponse(
                "An unexpected error occurred.",
                environment.IsDevelopment() ? exception.Message : null);

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }

    private sealed record ExceptionResponse(string Message, string? Detail);
}
