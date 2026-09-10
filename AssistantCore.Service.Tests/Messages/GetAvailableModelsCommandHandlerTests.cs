using AssistantCore.Service.Application.Commands.GetAvailableModels;
using AssistantCore.Service.Application.Models.Models;
using AssistantCore.Service.Application.Services.Models;

namespace AssistantCore.Service.Tests.Messages;

public sealed class GetAvailableModelsCommandHandlerTests
{
    [Fact]
    public async Task Given_ACatalogResponse_When_HandleAsync_Then_ReturnsItAsIs()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var expectedResponse = new GetAvailableModelsResponse(
            "gpt-5.6-luna",
            [new AvailableModelResponse("gpt-5.6-luna", "Luna", "Modele general.", true)]);
        var service = new StubModelCatalogService(expectedResponse);
        var handler = new GetAvailableModelsCommandHandler(service);

        // When
        var response = await handler.HandleAsync(new GetAvailableModelsCommand(), cancellationToken);

        // Then
        Assert.Same(expectedResponse, response);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }

    private sealed class StubModelCatalogService(GetAvailableModelsResponse response) : IModelCatalogService
    {
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<GetAvailableModelsResponse> GetAvailableModelsAsync(
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(response);
        }
    }
}
