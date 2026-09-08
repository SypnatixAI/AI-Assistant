namespace AssistantCore.Service.Application.Configuration;

public sealed class RagDiversificationOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Nombre de passages consecutifs tolere depuis une meme source avant de
    /// chercher une source differente. Au-dela, un passage d'une autre source
    /// remonte s'il reste assez proche en score.
    /// </summary>
    public int MaximumConsecutiveFromSameSource { get; init; } = 2;

    /// <summary>
    /// Ecart de score tolere pour promouvoir une source differente. Un passage
    /// nettement moins pertinent ne remonte jamais : la diversification ne doit
    /// pas contourner le classement.
    /// </summary>
    public double MaximumPromotionScoreGap { get; init; } = 0.2;

    /// <summary>
    /// Proportion de mots communs au-dela de laquelle deux passages de la meme
    /// source sont juges quasi identiques, le second etant alors ecarte.
    /// </summary>
    public double NearDuplicateOverlapThreshold { get; init; } = 0.9;
}
