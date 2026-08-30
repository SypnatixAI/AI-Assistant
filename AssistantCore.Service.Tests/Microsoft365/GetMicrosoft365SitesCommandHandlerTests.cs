using AssistantCore.Service.Application.Commands.GetMicrosoft365Sites;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class GetMicrosoft365SitesCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_AvailableSites_When_HandleAsync_Then_ReturnsSitesAndPropagatesCancellationToken(
        Microsoft365AvailableSiteResponse site,
        CancellationToken cancellationToken)
    {
        // Given
        var service = new StubSiteDiscoveryService { Sites = [site] };
        var handler = new GetMicrosoft365SitesCommandHandler(service);

        // When
        var response = await handler.HandleAsync(
            new GetMicrosoft365SitesCommand(),
            cancellationToken);

        // Then
        Assert.Same(site, Assert.Single(response.Sites));
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }

    private sealed class StubSiteDiscoveryService : IMicrosoft365SiteDiscoveryService
    {
        public IReadOnlyCollection<Microsoft365AvailableSiteResponse> Sites { get; init; } = [];
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IReadOnlyCollection<Microsoft365AvailableSiteResponse>> GetAvailableSitesAsync(
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(Sites);
        }
    }
}
