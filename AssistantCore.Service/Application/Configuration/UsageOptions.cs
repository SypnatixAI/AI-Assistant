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
}
