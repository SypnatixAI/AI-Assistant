using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent.Models;

namespace AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent;

public sealed record CompleteMicrosoft365ConsentCommand(
    string Code,
    string State,
    string? MicrosoftError) : IRequest<CompleteMicrosoft365ConsentResponse>;
