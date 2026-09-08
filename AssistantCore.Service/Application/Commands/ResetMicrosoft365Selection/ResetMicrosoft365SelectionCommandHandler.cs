using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.ResetMicrosoft365Selection.Models;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Application.Commands.ResetMicrosoft365Selection;

public sealed class ResetMicrosoft365SelectionCommandHandler(
    IMicrosoft365ResetService resetService)
    : IRequestHandler<ResetMicrosoft365SelectionCommand, ResetMicrosoft365SelectionResponse>
{
    public async Task<ResetMicrosoft365SelectionResponse> HandleAsync(
        ResetMicrosoft365SelectionCommand request,
        CancellationToken cancellationToken)
    {
        var result = await resetService.ResetSelectionAndIndexingAsync(cancellationToken);

        return new ResetMicrosoft365SelectionResponse(
            result.OrganizationId,
            result.DeletedSubscriptions,
            result.DeletedSynchronizations,
            result.DeletedWorkItems,
            result.DeletedIndexedContents,
            result.DeletedSources,
            result.DeletedSearchChunks);
    }
}
