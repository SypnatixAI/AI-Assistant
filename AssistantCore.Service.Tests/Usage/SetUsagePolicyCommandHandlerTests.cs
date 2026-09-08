using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Service.Application.Commands.SetUsagePolicy;

namespace AssistantCore.Service.Tests.Usage;

public sealed class SetUsagePolicyCommandHandlerTests
{
    [Fact]
    public async Task Given_ACreatedPolicy_When_HandleAsync_Then_MapsResponseAndPropagatesRequest()
    {
        // Given
        var cancellationToken = new CancellationTokenSource().Token;
        var policy = new OrganizationUsagePolicy
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Version = 3,
            MonthlyTokenLimit = 1_000_000,
            Status = UsagePolicyStatus.Active,
            EffectiveAt = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
            CreatedAt = DateTimeOffset.Parse("2026-08-18T15:00:00Z"),
            ActorId = Guid.NewGuid()
        };
        var service = new StubUsagePolicyManagementService { Policy = policy };
        var handler = new SetUsagePolicyCommandHandler(service);
        var command = new SetUsagePolicyCommand(
            policy.OrganizationId,
            1_000_000,
            "Active",
            "2026-09-01T00:00:00Z");

        // When
        var response = await handler.HandleAsync(command, cancellationToken);

        // Then
        Assert.Equal(policy.OrganizationId, response.OrganizationId);
        Assert.Equal(3, response.Version);
        Assert.Equal(1_000_000, response.MonthlyTokenLimit);
        Assert.Equal("Active", response.Status);
        Assert.Equal(policy.EffectiveAt, response.EffectiveAt);
        Assert.Equal(policy.CreatedAt, response.CreatedAt);
        Assert.Equal(policy.OrganizationId, service.ReceivedOrganizationId);
        Assert.Equal(1_000_000, service.ReceivedMonthlyTokenLimit);
        Assert.Equal("Active", service.ReceivedStatus);
        Assert.Equal("2026-09-01T00:00:00Z", service.ReceivedEffectiveAt);
        Assert.Equal(cancellationToken, service.ReceivedCancellationToken);
    }
}
