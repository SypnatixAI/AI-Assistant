using System.ClientModel;
using AssistantCore.ExternalServices.Entities.OpenAI.Models;
using OpenAI.Responses;

namespace AssistantCore.ExternalServices.Services.OpenAI;

#pragma warning disable OPENAI001
public sealed class OpenAiResponsesClient
{
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

    private static CreateResponseOptions CreateOptions(OpenAiResponsesRequest request)
    {
        var options = new CreateResponseOptions
        {
            Model = request.Model,
            Instructions = request.Instructions,
            ParallelToolCallsEnabled = true,
            StoredOutputEnabled = true,
            PreviousResponseId = request.PreviousResponseId
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
