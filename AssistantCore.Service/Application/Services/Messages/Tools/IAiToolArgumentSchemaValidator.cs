using System.Text.Json;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public interface IAiToolArgumentSchemaValidator
{
    void Validate(JsonElement arguments, JsonElement schema, string toolCallId);
}
