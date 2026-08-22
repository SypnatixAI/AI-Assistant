using System.Text.Json.Serialization;

namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record MicrosoftGraphNotificationCollection(
    [property: JsonPropertyName("value")] IReadOnlyCollection<MicrosoftGraphNotification>? Value);
