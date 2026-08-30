namespace AssistantCore.Service.Application.Configuration;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>
    /// Delai, en jours, pendant lequel une conversation supprimee reste recuperable
    /// avant que le travail de purge devienne eligible.
    /// </summary>
    public int ConversationRecoveryDays { get; init; }
}
