using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.StartMicrosoft365Consent.Models;

namespace AssistantCore.Service.Application.Commands.StartMicrosoft365Consent;

public sealed record StartMicrosoft365ConsentCommand : IRequest<StartMicrosoft365ConsentResponse>;
