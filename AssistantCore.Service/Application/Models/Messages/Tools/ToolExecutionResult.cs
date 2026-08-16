namespace AssistantCore.Service.Application.Models.Messages.Tools;

public sealed class ToolExecutionResult
{
    private const int MaximumToolCallIdLength = 100;
    private const int MaximumErrorCodeLength = 100;
    private const int MaximumWarningLength = 1000;

    private ToolExecutionResult(
        string toolCallId,
        ToolExecutionStatus status,
        IReadOnlyCollection<RetrievedEvidence> evidence,
        IReadOnlyCollection<string> warnings,
        string? errorCode)
    {
        ToolCallId = toolCallId;
        Status = status;
        Evidence = evidence;
        Warnings = warnings;
        ErrorCode = errorCode;
    }

    public string ToolCallId { get; }

    public ToolExecutionStatus Status { get; }

    public IReadOnlyCollection<RetrievedEvidence> Evidence { get; }

    public IReadOnlyCollection<string> Warnings { get; }

    public string? ErrorCode { get; }

    public static ToolExecutionResult Succeeded(
        string toolCallId,
        IReadOnlyCollection<RetrievedEvidence> evidence)
    {
        ValidateToolCallId(toolCallId);
        ArgumentNullException.ThrowIfNull(evidence);

        return new ToolExecutionResult(
            toolCallId,
            ToolExecutionStatus.Success,
            evidence.ToArray(),
            [],
            errorCode: null);
    }

    public static ToolExecutionResult PartiallySucceeded(
        string toolCallId,
        IReadOnlyCollection<RetrievedEvidence> evidence,
        IReadOnlyCollection<string> warnings)
    {
        ValidateToolCallId(toolCallId);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(warnings);

        if (evidence.Count == 0)
        {
            throw new ArgumentException(
                "A partially successful tool result must contain evidence.",
                nameof(evidence));
        }

        ValidateWarnings(warnings);
        if (warnings.Count == 0)
        {
            throw new ArgumentException(
                "A partially successful tool result must contain a warning.",
                nameof(warnings));
        }

        return new ToolExecutionResult(
            toolCallId,
            ToolExecutionStatus.PartialSuccess,
            evidence.ToArray(),
            warnings.ToArray(),
            errorCode: null);
    }

    public static ToolExecutionResult Failed(
        string toolCallId,
        string errorCode,
        IReadOnlyCollection<string>? warnings = null)
    {
        ValidateToolCallId(toolCallId);

        if (string.IsNullOrWhiteSpace(errorCode)
            || errorCode.Length > MaximumErrorCodeLength)
        {
            throw new ArgumentException(
                $"The error code must contain between 1 and {MaximumErrorCodeLength} characters.",
                nameof(errorCode));
        }

        var failureWarnings = warnings ?? [];
        ValidateWarnings(failureWarnings);

        return new ToolExecutionResult(
            toolCallId,
            ToolExecutionStatus.Failed,
            [],
            failureWarnings.ToArray(),
            errorCode);
    }

    private static void ValidateToolCallId(string toolCallId)
    {
        if (string.IsNullOrWhiteSpace(toolCallId)
            || toolCallId.Length > MaximumToolCallIdLength)
        {
            throw new ArgumentException(
                $"The tool call identifier must contain between 1 and {MaximumToolCallIdLength} characters.",
                nameof(toolCallId));
        }
    }

    private static void ValidateWarnings(IReadOnlyCollection<string> warnings)
    {
        if (warnings.Any(warning =>
                string.IsNullOrWhiteSpace(warning)
                || warning.Length > MaximumWarningLength))
        {
            throw new ArgumentException(
                $"Every warning must contain between 1 and {MaximumWarningLength} characters.",
                nameof(warnings));
        }
    }
}
