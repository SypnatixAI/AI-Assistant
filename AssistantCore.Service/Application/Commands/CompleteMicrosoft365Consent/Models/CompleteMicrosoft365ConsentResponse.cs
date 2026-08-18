namespace AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent.Models;

public sealed record CompleteMicrosoft365ConsentResponse(
    Guid ConnectionId,
    string TenantId,
    string Status);
