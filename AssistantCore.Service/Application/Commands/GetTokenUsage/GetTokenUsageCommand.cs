using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Usage;

namespace AssistantCore.Service.Application.Commands.GetTokenUsage;

public sealed record GetTokenUsageCommand : IRequest<TokenUsageResponse>;
