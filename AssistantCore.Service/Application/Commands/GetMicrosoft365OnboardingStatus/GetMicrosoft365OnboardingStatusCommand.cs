using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.GetMicrosoft365OnboardingStatus.Models;

namespace AssistantCore.Service.Application.Commands.GetMicrosoft365OnboardingStatus;

public sealed record GetMicrosoft365OnboardingStatusCommand
    : IRequest<GetMicrosoft365OnboardingStatusResponse>;
