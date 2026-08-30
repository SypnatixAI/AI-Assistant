using AssistantCore.Service.Application.Commands.RegisterMicrosoft365Site;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class RegisterMicrosoft365SiteCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_ASite_When_HandleAsync_Then_SelectsSiteAndReturnsResponse(
        string siteId,
        Microsoft365SiteResponse response,
        CancellationToken cancellationToken)
    {
        // Given
        var service = new StubSiteSelectionService { Response = response };
        var handler = new RegisterMicrosoft365SiteCommandHandler(service);
        var command = new RegisterMicrosoft365SiteCommand(siteId);

        // When
        var result = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Same(response, result);
        Assert.Equal(siteId, service.ReceivedSiteId);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }

    private sealed class StubSiteSelectionService : IMicrosoft365SiteSelectionService
    {
        public required Microsoft365SiteResponse Response { get; init; }
        public string? ReceivedSiteId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Microsoft365SiteResponse> SelectAsync(
            string siteId,
            CancellationToken cancellationToken = default)
        {
            ReceivedSiteId = siteId;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(Response);
        }
    }
}
