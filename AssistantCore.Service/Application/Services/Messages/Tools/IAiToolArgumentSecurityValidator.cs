using System.Text.Json;

namespace AssistantCore.Service.Application.Services.Messages.Tools;

public interface IAiToolArgumentSecurityValidator
{
    void Validate(JsonElement arguments, string toolCallId);
}
