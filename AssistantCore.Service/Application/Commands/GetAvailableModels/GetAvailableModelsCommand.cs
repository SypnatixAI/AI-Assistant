using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Models;

namespace AssistantCore.Service.Application.Commands.GetAvailableModels;

public sealed record GetAvailableModelsCommand : IRequest<GetAvailableModelsResponse>;
