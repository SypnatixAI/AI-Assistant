using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365ConnectionStateTests
{
    [Theory, AutoDomainData]
    public void Given_PendingConsent_When_Activate_Then_StatusBecomesActive(
        string tenantId,
        DateTimeOffset occurredAt)
    {
        // Given
        var connection = new Microsoft365Connection
        {
            Status = Microsoft365ConnectionStatus.PendingConsent
        };

        // When
        connection.Activate(tenantId, occurredAt);

        // Then
        Assert.Equal(Microsoft365ConnectionStatus.Active, connection.Status);
        Assert.Equal(tenantId, connection.TenantId);
        Assert.Equal(occurredAt, connection.ConsentValidatedAt);
    }

    [Theory, AutoDomainData]
    public void Given_Error_When_Activate_Then_TransitionIsRejected(
        string tenantId,
        DateTimeOffset occurredAt)
    {
        // Given
        var connection = new Microsoft365Connection
        {
            Status = Microsoft365ConnectionStatus.Error
        };

        // When
        var action = () => connection.Activate(tenantId, occurredAt);

        // Then
        Assert.Throws<InvalidOperationException>(action);
    }

    [Theory, AutoDomainData]
    public void Given_Revoked_When_MarkError_Then_TransitionIsRejected(
        string errorCode,
        DateTimeOffset occurredAt)
    {
        // Given
        var connection = new Microsoft365Connection
        {
            Status = Microsoft365ConnectionStatus.Revoked
        };

        // When
        var action = () => connection.MarkError(errorCode, occurredAt);

        // Then
        Assert.Throws<InvalidOperationException>(action);
    }
}
