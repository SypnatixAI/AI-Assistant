using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.CompleteMicrosoft365Consent;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class CompleteMicrosoft365ConsentCommandHandlerTests
{
    [Theory, AutoDomainData]
    public async Task Given_ACompletedConsent_When_HandleAsync_Then_FrontendRedirectIsReturned(
        CompleteMicrosoft365ConsentCommand command,
        Guid connectionId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Given
        var redirectUri = new Uri(
            "https://app.onpremia.example/microsoft365/consent/success");
        var service = new StubMicrosoft365ConnectionService
        {
            CompletionResult = new Microsoft365ConsentCompletionResult(
                connectionId,
                tenantId.ToString("D"),
                Microsoft365ConnectionStatus.Active,
                redirectUri)
        };
        var handler = new CompleteMicrosoft365ConsentCommandHandler(service);

        // When
        var response = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Equal(connectionId, response.ConnectionId);
        Assert.Equal(tenantId.ToString("D"), response.TenantId);
        Assert.Equal("Active", response.Status);
        Assert.Equal(redirectUri.AbsoluteUri, response.RedirectUrl);
        Assert.Equal(command, service.ReceivedCommand);
    }

    private sealed class StubMicrosoft365ConnectionService : IMicrosoft365ConnectionService
    {
        public required Microsoft365ConsentCompletionResult CompletionResult { get; init; }

        public CompleteMicrosoft365ConsentCommand? ReceivedCommand { get; private set; }

        public Task<Uri> StartConsentAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Microsoft365ConsentCompletionResult> CompleteConsentAsync(
            string tenantId,
            bool adminConsent,
            string state,
            string? microsoftError,
            CancellationToken cancellationToken = default)
        {
            ReceivedCommand = new CompleteMicrosoft365ConsentCommand(
                tenantId,
                adminConsent,
                state,
                microsoftError);
            return Task.FromResult(CompletionResult);
        }

        public Task<Microsoft365ConnectionResult> RevokeAsync(
            Guid connectionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
