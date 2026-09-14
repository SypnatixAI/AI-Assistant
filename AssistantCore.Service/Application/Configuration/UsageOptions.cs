namespace AssistantCore.Service.Application.Configuration;

public sealed class UsageOptions
{
    public const string SectionName = "Usage";

    /// <summary>
    /// Limite mensuelle de jetons appliquee a toute organisation, en attendant que
    /// les politiques de quota versionnees par organisation (ticket #108) remplacent
    /// cette valeur unique par une configuration commerciale reelle.
    /// </summary>
    public long DefaultMonthlyTokenLimit { get; init; }

    /// <summary>
    /// Frequence de resynchronisation du cache local de quota entre les instances API.
    /// La resynchronisation s'effectue en arriere-plan et n'ajoute aucun appel externe
    /// au chemin critique de POST /api/messages.
    /// </summary>
    public int CacheRefreshIntervalSeconds { get; init; } = 15;
}
