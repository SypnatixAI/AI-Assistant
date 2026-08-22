using System.Text.Json.Serialization;

namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record MicrosoftGraphNotification(
    [property: JsonPropertyName("subscriptionId")] string? SubscriptionId,
    [property: JsonPropertyName("clientState")] string? ClientState,
    [property: JsonPropertyName("tenantId")] string? TenantId,
    [property: JsonPropertyName("resource")] string? Resource);
