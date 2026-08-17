using System.Text;
using System.Text.Json;
using AssistantCore.Service.Application.Models.Messages.Tools;

namespace AssistantCore.Service.Application.Services.Messages.Orchestration;

public sealed class ToolCallFingerprintGenerator : IToolCallFingerprintGenerator
{
    public string CreateFingerprint(AiRequestedToolCall toolCall)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteJsonWithSortedProperties(writer, toolCall.Arguments);
        }

        return $"{toolCall.ToolName}\n{Encoding.UTF8.GetString(stream.ToArray())}";
    }

    private static void WriteJsonWithSortedProperties(
        Utf8JsonWriter writer,
        JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value
                             .EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteJsonWithSortedProperties(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteJsonWithSortedProperties(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
