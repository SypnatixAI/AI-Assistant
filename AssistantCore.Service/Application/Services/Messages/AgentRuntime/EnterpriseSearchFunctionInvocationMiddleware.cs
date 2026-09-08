using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Orchestration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

internal sealed class EnterpriseSearchFunctionInvocationMiddleware(
    MessageOrchestrationOptions options,
    IToolCallFingerprintGenerator fingerprintGenerator,
    ILogger<EnterpriseSearchFunctionInvocationMiddleware> logger)
{
    private const string EnterpriseSearchFunctionName = "EnterpriseSearch";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<string, int> _fingerprintCounts = new(StringComparer.Ordinal);
    private readonly object _stateLock = new();
    private int _toolCallCount;

    public async ValueTask<object> InvokeAsync(
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(context.Function.Name, EnterpriseSearchFunctionName, StringComparison.Ordinal))
        {
            return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
        }

        var requestedToolCall = CreateRequestedToolCall(context);
        var fingerprint = fingerprintGenerator.CreateFingerprint(requestedToolCall);
        var decision = RecordInvocation(fingerprint);

        if (!decision.IsAllowed)
        {
            context.Terminate = true;
            logger.LogWarning(
                "EnterpriseSearch function-call loop stopped because {Reason} exceeded the configured limit.",
                decision.Reason);

            return EnterpriseSearchAgentTool.EnterpriseSearchToolResult.Stopped(decision.ErrorCode);
        }

        return await context.Function.InvokeAsync(context.Arguments, cancellationToken);
    }

    private InvocationDecision RecordInvocation(string fingerprint)
    {
        lock (_stateLock)
        {
            _toolCallCount++;
            var repeatedCallCount = _fingerprintCounts.GetValueOrDefault(fingerprint) + 1;
            _fingerprintCounts[fingerprint] = repeatedCallCount;

            if (_toolCallCount > options.MaximumToolCalls)
            {
                return InvocationDecision.Denied(
                    "maximum tool calls",
                    ToolExecutionErrorCodes.MaximumToolCallsExceeded);
            }

            if (repeatedCallCount > options.MaximumRepeatedToolCalls)
            {
                return InvocationDecision.Denied(
                    "maximum repeated tool calls",
                    ToolExecutionErrorCodes.MaximumRepeatedToolCallsExceeded);
            }

            return InvocationDecision.Allowed;
        }
    }

    private static AiRequestedToolCall CreateRequestedToolCall(
        FunctionInvocationContext context) =>
        new(
            context.CallContent.CallId,
            EnterpriseSearchFunctionName,
            JsonSerializer.SerializeToElement(context.Arguments, SerializerOptions));

    private sealed record InvocationDecision(
        bool IsAllowed,
        string Reason,
        string ErrorCode)
    {
        public static InvocationDecision Allowed { get; } =
            new(true, string.Empty, string.Empty);

        public static InvocationDecision Denied(string reason, string errorCode) =>
            new(false, reason, errorCode);
    }
}
