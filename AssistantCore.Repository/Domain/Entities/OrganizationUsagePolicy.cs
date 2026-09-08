using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Repository.Domain.Entities;

/// <summary>
/// Version historisee de la politique de quota d'une organisation. Chaque changement
/// insere une nouvelle ligne ; aucune ligne existante n'est jamais modifiee, pour que les
/// periodes de consommation passees restent evaluees avec la politique alors en vigueur.
/// </summary>
public sealed class OrganizationUsagePolicy
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public int Version { get; set; }

    public long MonthlyTokenLimit { get; set; }

    public UsagePolicyStatus Status { get; set; }

    public DateTimeOffset EffectiveAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid ActorId { get; set; }

    public Organization Organization { get; set; } = null!;
}
