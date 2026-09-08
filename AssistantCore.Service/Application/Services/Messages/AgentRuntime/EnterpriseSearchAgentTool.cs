using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.Service.Application.Models.Messages;
using AssistantCore.Service.Application.Models.Messages.Connectors;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Tools;
using Microsoft.Extensions.AI;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

internal sealed class EnterpriseSearchAgentTool(
    AiToolDefinition authorizedTool,
    ConnectorExecutionContext executionContext,
    IAiToolCallValidator toolCallValidator,
    IToolExecutionRouter toolExecutionRouter)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly List<ToolExecutionResult> _executedResults = [];
    private readonly object _executedResultsLock = new();

    public AIFunction CreateFunction() =>
        AIFunctionFactory.Create(
            (Func<string, CancellationToken, Task<EnterpriseSearchToolResult>>)SearchAsync,
            new AIFunctionFactoryOptions
            {
                Name = "EnterpriseSearch",
                Description = "Search authorized internal enterprise documents when the answer depends on organization-specific information."
            });

    public IReadOnlyCollection<ToolExecutionResult> ExecutedResults
    {
        get
        {
            lock (_executedResultsLock)
            {
                return _executedResults.ToArray();
            }
        }
    }

    private async Task<EnterpriseSearchToolResult> SearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var arguments = JsonSerializer.SerializeToElement(
            new
            {
                query,
                sourceTypes = (IReadOnlyCollection<string>?)null,
                dateFrom = (string?)null,
                dateTo = (string?)null
            },
            SerializerOptions);
        var requestedToolCall = new AiRequestedToolCall(
            $"enterprise-search-{Guid.NewGuid():N}",
            authorizedTool.Name,
            arguments);
        var validatedToolCall = await toolCallValidator.ValidateAsync(
            requestedToolCall,
            [authorizedTool],
            cancellationToken);
        var result = await toolExecutionRouter.ExecuteAsync(
            validatedToolCall,
            executionContext,
            cancellationToken);

        lock (_executedResultsLock)
        {
            _executedResults.Add(result);
        }

        return EnterpriseSearchToolResult.From(result);
    }

    internal sealed record EnterpriseSearchToolResult(
        ToolExecutionStatus Status,
        IReadOnlyCollection<RetrievedEvidence> Evidence,
        IReadOnlyCollection<string> Warnings,
        string? ErrorCode)
    {
        public static EnterpriseSearchToolResult From(ToolExecutionResult result) =>
            new(
                result.Status,
                result.Evidence,
                result.Warnings,
                result.ErrorCode);

        public static EnterpriseSearchToolResult Stopped(string errorCode) =>
            new(
                ToolExecutionStatus.Failed,
                [],
                ["EnterpriseSearch stopped because the function-call loop limit was reached."],
                errorCode);
    }
}
