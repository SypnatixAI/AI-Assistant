using System.ClientModel;
using System.Text;
using AssistantCore.ExternalServices.Entities.OpenAI.Models;
using OpenAI.Responses;

namespace AssistantCore.ExternalServices.Services.OpenAI;

#pragma warning disable OPENAI001
public sealed class OpenAiResponsesClient
{
    private const string DecisionOutputSchema =
        """
        {
          "type": "object",
          "properties": {
            "decision": {
              "type": "string",
              "enum": ["answer", "askClarification", "cannotAnswer"]
            },
            "reason": {
              "type": "string"
            },
            "answer": {
              "type": "string"
            },
            "progressMessage": {
              "type": ["string", "null"]
            },
            "evidenceIds": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          "required": ["decision", "reason", "answer", "progressMessage", "evidenceIds"],
          "additionalProperties": false
        }
        """;

    private readonly ResponsesClient _client;

    public OpenAiResponsesClient(OpenAiClientSettings settings)
    {
        _client = new ResponsesClient(
            new ApiKeyCredential(settings.ApiKey),
            new ResponsesClientOptions
            {
                Endpoint = new Uri(settings.Endpoint, UriKind.Absolute)
            });
    }

    public async Task<OpenAiResponsesResult> CreateResponseAsync(
        OpenAiResponsesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = CreateOptions(request);
            var result = await _client.CreateResponseAsync(options, cancellationToken);
            var response = result.Value;

            if (response.Error is not null)
            {
                throw new OpenAiExternalException(502);
            }

            var toolCalls = response.OutputItems
                .OfType<FunctionCallResponseItem>()
                .Select(toolCall => new OpenAiToolCall(
                    toolCall.CallId,
                    toolCall.FunctionName,
                    toolCall.FunctionArguments.ToString()))
                .ToArray();

            return new OpenAiResponsesResult(
                response.Id,
                response.GetOutputText(),
                toolCalls,
                response.Usage?.InputTokenCount ?? 0,
                response.Usage?.OutputTokenCount ?? 0);
        }
        catch (ClientResultException exception)
        {
            throw new OpenAiExternalException(exception.Status);
        }
    }

    public async Task<OpenAiResponsesResult> CreateResponseStreamingAsync(
        OpenAiResponsesRequest request,
        Func<string, CancellationToken, ValueTask> onTextDelta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onTextDelta);

        try
        {
            var options = CreateOptions(request, streamingEnabled: true);
            var outputText = new StringBuilder();
            IReadOnlyCollection<OpenAiToolCall> toolCalls = [];
            string? responseId = null;
            var inputTokens = 0;
            var outputTokens = 0;

            await foreach (var update in _client.CreateResponseStreamingAsync(options, cancellationToken))
            {
                switch (update)
                {
                    case StreamingResponseOutputTextDeltaUpdate textDelta:
                        outputText.Append(textDelta.Delta);
                        await onTextDelta(textDelta.Delta, cancellationToken);
                        break;
                    case StreamingResponseCompletedUpdate completed:
                        if (completed.Response.Error is not null)
                        {
                            throw new OpenAiExternalException(502);
                        }

                        responseId = completed.Response.Id;
                        inputTokens = completed.Response.Usage?.InputTokenCount ?? 0;
                        outputTokens = completed.Response.Usage?.OutputTokenCount ?? 0;
                        toolCalls = completed.Response.OutputItems
                            .OfType<FunctionCallResponseItem>()
                            .Select(toolCall => new OpenAiToolCall(
                                toolCall.CallId,
                                toolCall.FunctionName,
                                toolCall.FunctionArguments.ToString()))
                            .ToArray();
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(responseId))
            {
                throw new OpenAiExternalException(502);
            }

            return new OpenAiResponsesResult(
                responseId,
                outputText.ToString(),
                toolCalls,
                inputTokens,
                outputTokens);
        }
        catch (ClientResultException exception)
        {
            throw new OpenAiExternalException(exception.Status);
        }
    }

    public async Task<string> CreateConversationSummaryAsync(
        OpenAiConversationSummaryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new CreateResponseOptions
            {
                Model = request.Model,
                Instructions = request.Instructions,
                StoredOutputEnabled = true
            };

            foreach (var message in request.ConversationHistory)
            {
                options.InputItems.Add(message.Role switch
                {
                    OpenAiConversationRole.User => ResponseItem.CreateUserMessageItem(message.Content),
                    OpenAiConversationRole.Assistant => ResponseItem.CreateAssistantMessageItem(message.Content),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(request), message.Role, "Unsupported conversation role.")
                });
            }

            options.InputItems.Add(ResponseItem.CreateUserMessageItem(
                $"Current user message:\n{request.CurrentUserMessage}\n\n" +
                $"Current assistant response:\n{request.CurrentAssistantMessage}"));

            var result = await _client.CreateResponseAsync(options, cancellationToken);
            if (result.Value.Error is not null)
            {
                throw new OpenAiExternalException(502);
            }

            return result.Value.GetOutputText();
        }
        catch (ClientResultException exception)
        {
            throw new OpenAiExternalException(exception.Status);
        }
    }

    private static CreateResponseOptions CreateOptions(
        OpenAiResponsesRequest request,
        bool streamingEnabled = false)
    {
        var options = new CreateResponseOptions
        {
            Model = request.Model,
            Instructions = request.Instructions,
            ParallelToolCallsEnabled = true,
            StoredOutputEnabled = true,
            StreamingEnabled = streamingEnabled,
            PreviousResponseId = request.PreviousResponseId,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "assistant_decision",
                    BinaryData.FromString(DecisionOutputSchema),
                    "The assistant's final answerability decision.",
                    true)
            }
        };

        if (request.PreviousResponseId is null)
        {
            foreach (var message in request.ConversationHistory)
            {
                options.InputItems.Add(message.Role switch
                {
                    OpenAiConversationRole.User =>
                        ResponseItem.CreateUserMessageItem(message.Content),
                    OpenAiConversationRole.Assistant =>
                        ResponseItem.CreateAssistantMessageItem(message.Content),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(request),
                        message.Role,
                        "Unsupported conversation role.")
                });
            }

            options.InputItems.Add(ResponseItem.CreateUserMessageItem(request.UserMessage));
        }

        if (request.PreviousResponseId is null && request.ToolResults.Count > 0)
        {
            throw new InvalidOperationException(
                "Tool results require a previous OpenAI response identifier.");
        }

        foreach (var toolResult in request.ToolResults)
        {
            options.InputItems.Add(ResponseItem.CreateFunctionCallOutputItem(
                toolResult.ToolCallId,
                toolResult.ResultJson));
        }

        foreach (var tool in request.AvailableTools)
        {
            options.Tools.Add(ResponseTool.CreateFunctionTool(
                tool.Name,
                BinaryData.FromString(tool.InputSchemaJson),
                true,
                tool.Description));
        }

        return options;
    }
}
#pragma warning restore OPENAI001
