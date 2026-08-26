using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace AssistantCore.Service.Infrastructure.Connectors;

public sealed class ScopedToolExecutionRouter(IServiceScopeFactory scopeFactory)
    : IToolExecutionRouter
{
    public async Task<ToolExecutionResult> ExecuteAsync(
        ValidatedToolCall toolCall,
        ConnectorExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolCall);
        ArgumentNullException.ThrowIfNull(executionContext);
        cancellationToken.ThrowIfCancellationRequested();

        await using var executionScope = scopeFactory.CreateAsyncScope();
        var handlers = executionScope.ServiceProvider
            .GetServices<IAiToolExecutionHandler>();
        var router = new ToolExecutionRouter(handlers);

        return await router.ExecuteAsync(
            toolCall,
            executionContext,
            cancellationToken);
    }
}
