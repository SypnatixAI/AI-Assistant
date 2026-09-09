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
        new EnterpriseSearchFunction(AIFunctionFactory.Create(
            (Func<AIFunctionArguments, CancellationToken, Task<EnterpriseSearchToolResult>>)SearchAsync,
            new AIFunctionFactoryOptions
            {
                Name = "EnterpriseSearch",
                Description = "Search authorized internal enterprise documents when the answer depends on organization-specific information."
            }),
            authorizedTool.InputSchema);

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
        AIFunctionArguments functionArguments,
        CancellationToken cancellationToken)
    {
        var enterpriseSearchArguments = DeserializeArguments(functionArguments);
        var arguments = JsonSerializer.SerializeToElement(
            enterpriseSearchArguments,
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

    private static EnterpriseSearchArguments DeserializeArguments(
        AIFunctionArguments functionArguments)
    {
        var argumentValues = functionArguments.ToDictionary(
            argument => argument.Key,
            argument => argument.Value,
            StringComparer.Ordinal);
        var argumentsJson = JsonSerializer.SerializeToElement(
            argumentValues,
            SerializerOptions);

        return argumentsJson.Deserialize<EnterpriseSearchArguments>(SerializerOptions)
            ?? throw new InvalidOperationException("The enterprise search arguments are invalid.");
    }

    internal sealed record EnterpriseSearchArguments(
        string Query,
        IReadOnlyCollection<string>? SourceTypes,
        string? DateFrom,
        string? DateTo);

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

    private sealed class EnterpriseSearchFunction(
        AIFunction innerFunction,
        JsonElement inputSchema) : DelegatingAIFunction(innerFunction)
    {
        public override JsonElement JsonSchema => inputSchema;
    }
}
