using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Commands.EnableMicrosoft365List;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class EnableMicrosoft365ListCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AList_When_HandleAsync_Then_MapsResponseAndPropagatesCancellationToken(
        Microsoft365List list,
        CancellationToken cancellationToken)
    {
        // Given
        var service = new StubMicrosoft365ListActivationService { List = list };
        var handler = new EnableMicrosoft365ListCommandHandler(service);
        var command = new EnableMicrosoft365ListCommand(list.SiteId, list.ListId, true);

        // When
        var response = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Equal(list.SiteId, response.SiteId);
        Assert.Equal(list.ListId, response.ListId);
        Assert.Equal(list.DisplayName, response.DisplayName);
        Assert.Equal(list.WebUrl, response.WebUrl);
        Assert.Equal(list.Status.ToString(), response.Status);
        Assert.Equal(list.IsIndexed, response.IsIndexed);
        Assert.Equal(command.SiteId, service.ReceivedSiteId);
        Assert.Equal(command.ListId, service.ReceivedListId);
        Assert.True(service.ReceivedIsIndexed);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }

    private sealed class StubMicrosoft365ListActivationService : IMicrosoft365ListActivationService
    {
        public required Microsoft365List List { get; init; }
        public string? ReceivedSiteId { get; private set; }
        public string? ReceivedListId { get; private set; }
        public bool ReceivedIsIndexed { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Microsoft365List> SetIndexingAsync(
            string siteId,
            string listId,
            bool isIndexed,
            CancellationToken cancellationToken = default)
        {
            ReceivedSiteId = siteId;
            ReceivedListId = listId;
            ReceivedIsIndexed = isIndexed;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(List);
        }
    }
}
