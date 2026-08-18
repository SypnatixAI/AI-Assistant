using System.Text.Json;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public interface IAiToolDateRangeValidator
{
    void Validate(JsonElement arguments, string toolCallId);
}
