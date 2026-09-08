using System.Text.Json;
using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Repositories.Audit;

public static class AdministrativeAuditEntryFactory
{
    public static AdministrativeAuditEntry Create(
        Guid organizationId,
        string actorType,
        Guid actorId,
        AdministrativeAuditAction action,
        string targetType,
        Guid targetId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, object?> oldValues,
        IReadOnlyDictionary<string, object?> newValues,
        string correlationId)
    {
        AdministrativeAuditFieldWhitelist.Validate(action, oldValues);
        AdministrativeAuditFieldWhitelist.Validate(action, newValues);

        return new AdministrativeAuditEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ActorType = actorType,
            ActorId = actorId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            OccurredAt = occurredAt,
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(newValues),
            CorrelationId = correlationId
        };
    }
}
