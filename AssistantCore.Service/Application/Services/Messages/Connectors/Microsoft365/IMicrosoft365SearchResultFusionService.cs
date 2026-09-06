using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;

namespace AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;

public interface IMicrosoft365SearchResultFusionService
{
    IReadOnlyCollection<Microsoft365SearchRecord> Fuse(
        IReadOnlyCollection<IReadOnlyCollection<Microsoft365SearchRecord>> resultSets,
        int maximumResults);
}
