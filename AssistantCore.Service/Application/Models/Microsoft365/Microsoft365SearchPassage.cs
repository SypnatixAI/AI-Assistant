namespace AssistantCore.Service.Application.Models.Microsoft365;

/// <summary>
/// <paramref name="Content"/> reste le texte du document, sans prefixe artificiel :
/// c'est lui qui sera cite a l'utilisateur. Le contexte hierarchique voyage a cote,
/// dans <paramref name="SectionTitle"/>, et n'est assemble qu'au moment de calculer
/// l'embedding. Melanger les deux ferait apparaitre "Section: ..." dans les citations
/// et amputerait le texte reel d'autant de caracteres.
/// </summary>
public sealed record Microsoft365SearchPassage(
    string ChunkId,
    string Title,
    string Content,
    string? SiteId = null,
    string? DriveId = null,
    string? DriveItemId = null,
    string? DocumentVersion = null,
    int ChunkNumber = 0,
    string? Url = null,
    DateTimeOffset? ModifiedAt = null,
    IReadOnlyList<float>? ContentVector = null,
    string SourceType = "sharepoint",
    string? SectionTitle = null);
