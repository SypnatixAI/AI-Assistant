namespace AssistantCore.Repository.Domain.Entities;

public sealed class Microsoft365IndexedPassage
{
    public Guid Id { get; set; }
    public Guid Microsoft365IndexedContentId { get; set; }
    public string ChunkId { get; set; } = string.Empty;
    public Microsoft365IndexedContent Microsoft365IndexedContent { get; set; } = null!;
}
