using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.GetMicrosoft365SiteLists;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class GetMicrosoft365SiteListsCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_DiscoveredLists_When_HandleAsync_Then_MapsResponseAndPropagatesCancellationToken(
        Microsoft365List list,
        CancellationToken cancellationToken)
    {
        // Given
        list.Status = Microsoft365SourceStatus.Discovered;
        var service = new StubMicrosoft365ListConsultationService { Lists = [list] };
        var handler = new GetMicrosoft365SiteListsCommandHandler(service);

        // When
        var response = await handler.HandleAsync(
            new GetMicrosoft365SiteListsCommand(list.SiteId),
            cancellationToken);

        // Then
        var mappedList = Assert.Single(response.Lists);
        Assert.Equal(list.SiteId, mappedList.SiteId);
        Assert.Equal(list.ListId, mappedList.ListId);
        Assert.Equal(list.DisplayName, mappedList.DisplayName);
        Assert.Equal(list.WebUrl, mappedList.WebUrl);
        Assert.Equal(list.Status.ToString(), mappedList.Status);
        Assert.Equal(list.IsIndexed, mappedList.IsIndexed);
        Assert.Equal(list.SiteId, service.ReceivedSiteId);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }

    private sealed class StubMicrosoft365ListConsultationService : IMicrosoft365ListConsultationService
    {
        public IReadOnlyCollection<Microsoft365List> Lists { get; init; } = [];
        public string? ReceivedSiteId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IReadOnlyCollection<Microsoft365List>> GetListsAsync(
            string siteId,
            CancellationToken cancellationToken = default)
        {
            ReceivedSiteId = siteId;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(Lists);
        }
    }
}
