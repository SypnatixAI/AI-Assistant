using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Usage;
using AssistantCore.Service.Application.Services.Messages.Authorization;
using AssistantCore.Service.Application.Services.Usage;

namespace AssistantCore.Service.Application.Commands.GetTokenUsage;

public sealed class GetTokenUsageCommandHandler(
    IMessageUserContextService userContextService,
    IUsageTrackingService usageTrackingService)
    : IRequestHandler<GetTokenUsageCommand, TokenUsageResponse>
{
    public async Task<TokenUsageResponse> HandleAsync(
        GetTokenUsageCommand request,
        CancellationToken cancellationToken)
    {
        var userContext = await userContextService.GetCurrentAsync(cancellationToken);

        return await usageTrackingService.GetCurrentUsageAsync(
            userContext.Organization.Id,
            cancellationToken);
    }
}
