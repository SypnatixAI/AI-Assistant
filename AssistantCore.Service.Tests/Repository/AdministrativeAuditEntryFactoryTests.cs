using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories.Audit;

namespace AssistantCore.Service.Tests.Repository;

public sealed class AdministrativeAuditEntryFactoryTests
{
    [Theory]
    [InlineData(AdministrativeAuditAction.MemberRoleChanged, "role")]
    [InlineData(AdministrativeAuditAction.MemberStatusChanged, "status")]
    [InlineData(AdministrativeAuditAction.UsagePolicyChanged, "monthlyTokenLimit")]
    [InlineData(AdministrativeAuditAction.UsagePolicyChanged, "status")]
    [InlineData(AdministrativeAuditAction.UsagePolicyChanged, "effectiveAt")]
    [InlineData(AdministrativeAuditAction.UsagePolicyChanged, "version")]
    [InlineData(AdministrativeAuditAction.ConversationArchived, "status")]
    [InlineData(AdministrativeAuditAction.ConversationDeleted, "deletedAt")]
    [InlineData(AdministrativeAuditAction.ConnectorStatusChanged, "status")]
    [InlineData(AdministrativeAuditAction.ConnectorStatusChanged, "isIndexed")]
    public void Given_AWhitelistedField_When_Create_Then_ReturnsAnEntryWithSerializedValues(
        AdministrativeAuditAction action,
        string field)
    {
        // Given
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        // When
        var entry = AdministrativeAuditEntryFactory.Create(
            organizationId,
            "Member",
            actorId,
            action,
            "Member",
            targetId,
            occurredAt,
            new Dictionary<string, object?> { [field] = "before" },
            new Dictionary<string, object?> { [field] = "after" },
            "request-8f812");

        // Then
        Assert.Equal(organizationId, entry.OrganizationId);
        Assert.Equal(actorId, entry.ActorId);
        Assert.Equal(action, entry.Action);
        Assert.Equal(targetId, entry.TargetId);
        Assert.Equal(occurredAt, entry.OccurredAt);
        Assert.Equal("request-8f812", entry.CorrelationId);
        Assert.Contains($"\"{field}\":\"before\"", entry.OldValues);
        Assert.Contains($"\"{field}\":\"after\"", entry.NewValues);
    }

    [Fact]
    public void Given_AFieldNotInTheWhitelist_When_CreateWithOldValues_Then_Throws()
    {
        // Given / When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AdministrativeAuditEntryFactory.Create(
                Guid.NewGuid(),
                "Member",
                Guid.NewGuid(),
                AdministrativeAuditAction.MemberStatusChanged,
                "Member",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                new Dictionary<string, object?> { ["email"] = "bob@contoso.com" },
                new Dictionary<string, object?> { ["status"] = "Inactive" },
                "request-8f812"));

        // Then
        Assert.Contains("email", exception.Message);
    }

    [Fact]
    public void Given_AFieldNotInTheWhitelist_When_CreateWithNewValues_Then_Throws()
    {
        // Given / When
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AdministrativeAuditEntryFactory.Create(
                Guid.NewGuid(),
                "Member",
                Guid.NewGuid(),
                AdministrativeAuditAction.MemberRoleChanged,
                "Member",
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                new Dictionary<string, object?> { ["role"] = "User" },
                new Dictionary<string, object?> { ["role"] = "Admin", ["name"] = "Bob" },
                "request-8f812"));

        // Then
        Assert.Contains("name", exception.Message);
    }
}
