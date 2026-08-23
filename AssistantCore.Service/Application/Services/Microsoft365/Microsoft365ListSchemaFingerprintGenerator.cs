using System.Security.Cryptography;
using System.Text.Json;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ListSchemaFingerprintGenerator
    : IMicrosoft365ListSchemaFingerprintGenerator
{
    public string CreateFingerprint(IReadOnlyCollection<Microsoft365ListColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        using var canonicalSchema = new MemoryStream();
        using (var writer = new Utf8JsonWriter(canonicalSchema))
        {
            writer.WriteStartArray();
            foreach (var column in columns.OrderBy(column => column.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", column.Id);
                writer.WritePropertyName("definition");
                WriteCanonicalValue(writer, column.Definition);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Convert.ToHexString(SHA256.HashData(canonicalSchema.ToArray()));
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalValue(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new ArgumentException("A list column contains an unsupported JSON value.", nameof(value));
        }
    }
}
