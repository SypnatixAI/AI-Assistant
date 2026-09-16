using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Messages.Tools;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiToolCallValidatorTests
{
    [Theory, AutoDomainData]
    public async Task Given_AValidRegisteredTool_When_ValidateAsync_Then_ReturnsValidatedCall(Guid callId)
    {
        // Given
        var requestedToolCall = CreateReadOnlyCall(callId.ToString());
        var validator = CreateValidator();

        // When
        var result = await validator.ValidateAsync(
            requestedToolCall,
            [CreateReadOnlyTool()],
            CancellationToken.None);

        // Then
        Assert.Equal(requestedToolCall.CallId, result.CallId);
        Assert.Equal(AiToolNames.AnalyzeMicrosoft365Spreadsheet, result.ToolName);
        Assert.Equal(requestedToolCall.Arguments.GetRawText(), result.Arguments.GetRawText());
    }

    [Theory, AutoDomainData]
    public async Task Given_AnUnknownTool_When_ValidateAsync_Then_RejectsCall(Guid callId)
    {
        // Given
        var requestedToolCall = CreateReadOnlyCall(callId.ToString(), toolName: "unknown_tool");
        var validator = CreateValidator();

        // When
        var exception = await Assert.ThrowsAsync<ToolCallValidationException>(() =>
            validator.ValidateAsync(requestedToolCall, [CreateReadOnlyTool()], CancellationToken.None));

        // Then
        Assert.Equal(callId.ToString(), exception.ToolCallId);
    }

    [Theory, AutoDomainData]
    public async Task Given_ARegisteredWriteTool_When_ValidateAsync_Then_RejectsCall(Guid callId)
    {
        // Given
        const string writeToolName = "delete_erp_invoice";
        var requestedToolCall = CreateReadOnlyCall(callId.ToString(), toolName: writeToolName);
        var validator = CreateValidator();

        // When
        var exception = await Assert.ThrowsAsync<ToolCallValidationException>(() =>
            validator.ValidateAsync(
                requestedToolCall,
                [CreateReadOnlyTool(writeToolName)],
                CancellationToken.None));

        // Then
        Assert.Contains("read-only", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_AMissingRequiredField_When_ValidateAsync_Then_RejectsCall(Guid callId)
    {
        // Given
        var arguments = CreateValidArguments();
        arguments.Remove("query");
        var requestedToolCall = CreateReadOnlyCall(callId.ToString(), arguments);
        var validator = CreateValidator();

        // When
        var exception = await Assert.ThrowsAsync<ToolCallValidationException>(() =>
            validator.ValidateAsync(requestedToolCall, [CreateReadOnlyTool()], CancellationToken.None));

        // Then
        Assert.Contains("query", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineAutoDomainData("url")]
    [InlineAutoDomainData("token")]
    [InlineAutoDomainData("apiKey")]
    [InlineAutoDomainData("organizationId")]
    public async Task Given_ATechnicalField_When_ValidateAsync_Then_RejectsCall(
        string technicalFieldName,
        Guid callId)
    {
        // Given
        var arguments = CreateValidArguments();
        arguments[technicalFieldName] = "untrusted-value";
        var requestedToolCall = CreateReadOnlyCall(callId.ToString(), arguments);
        var validator = CreateValidator();

        // When
        var exception = await Assert.ThrowsAsync<ToolCallValidationException>(() =>
            validator.ValidateAsync(requestedToolCall, [CreateReadOnlyTool()], CancellationToken.None));

        // Then
        Assert.Contains("Technical field", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineAutoDomainData("2026-02-30", "2026-03-01")]
    [InlineAutoDomainData("2026-06-30", "2026-04-01")]
    public async Task Given_AnInvalidDateRange_When_ValidateAsync_Then_RejectsCall(
        string dateFrom,
        string dateTo,
        Guid callId)
    {
        // Given
        var arguments = CreateValidArguments();
        arguments["dateFrom"] = dateFrom;
        arguments["dateTo"] = dateTo;
        var requestedToolCall = CreateReadOnlyCall(callId.ToString(), arguments);
        var validator = CreateValidator();

        // When
        var exception = await Assert.ThrowsAsync<ToolCallValidationException>(() =>
            validator.ValidateAsync(requestedToolCall, [CreateReadOnlyTool()], CancellationToken.None));

        // Then
        Assert.Equal(callId.ToString(), exception.ToolCallId);
    }

    [Theory, AutoDomainData]
    public async Task Given_AnOversizedString_When_ValidateAsync_Then_RejectsCall(Guid callId)
    {
        // Given
        var arguments = CreateValidArguments();
        arguments["query"] = new string('a', 501);
        var validator = CreateValidator();

        // When
        var exception = await Assert.ThrowsAsync<ToolCallValidationException>(() =>
            validator.ValidateAsync(
                CreateReadOnlyCall(callId.ToString(), arguments),
                [CreateReadOnlyTool()],
                CancellationToken.None));

        // Then
        Assert.Contains("500", exception.Message, StringComparison.Ordinal);
    }

    [Theory, AutoDomainData]
    public async Task Given_ACancelledRequest_When_ValidateAsync_Then_PropagatesCancellation(Guid callId)
    {
        // Given
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var validator = CreateValidator();

        // When
        var exception = await Record.ExceptionAsync(() =>
            validator.ValidateAsync(
                CreateReadOnlyCall(callId.ToString()),
                [CreateReadOnlyTool()],
                cancellationSource.Token));

        // Then
        Assert.IsType<OperationCanceledException>(exception);
    }

    private static AiRequestedToolCall CreateReadOnlyCall(
        string callId,
        IReadOnlyDictionary<string, object?>? arguments = null,
        string toolName = AiToolNames.AnalyzeMicrosoft365Spreadsheet) => new(
            callId,
            toolName,
            JsonSerializer.SerializeToElement(arguments ?? CreateValidArguments()));

    private static AiToolCallValidator CreateValidator() => new(
        new AiToolArgumentSchemaValidator(),
        new AiToolArgumentSecurityValidator(),
        new AiToolDateRangeValidator());

    private static Dictionary<string, object?> CreateValidArguments() => new()
    {
        ["query"] = "rapport ventes trimestre",
        ["dateFrom"] = "2026-04-01",
        ["dateTo"] = "2026-06-30"
    };

    private static AiToolDefinition CreateReadOnlyTool(
        string toolName = AiToolNames.AnalyzeMicrosoft365Spreadsheet) => new(
            toolName,
            "Read-only validation test.",
            ParseJson(
                """
                {
                  "type": "object",
                  "properties": {
                    "query": { "type": "string" },
                    "dateFrom": { "anyOf": [{ "type": "string" }, { "type": "null" }] },
                    "dateTo": { "anyOf": [{ "type": "string" }, { "type": "null" }] }
                  },
                  "required": ["query", "dateFrom", "dateTo"],
                  "additionalProperties": false
                }
                """));

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
