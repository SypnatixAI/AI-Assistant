using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

/// <summary>
/// Trace immuable d'une action administrative sensible. Ne contient jamais de token,
/// de claims complets, de contenu de conversation ou de secret de connecteur.
/// </summary>
public sealed class AdministrativeAuditEntry
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string ActorType { get; set; } = string.Empty;

    public Guid ActorId { get; set; }

    public AdministrativeAuditAction Action { get; set; }

    public string TargetType { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string OldValues { get; set; } = string.Empty;

    public string NewValues { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public Organization Organization { get; set; } = null!;
}
