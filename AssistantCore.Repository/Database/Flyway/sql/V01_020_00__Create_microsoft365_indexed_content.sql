USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[Microsoft365IndexedContent]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365IndexedContent]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365SourceId] UNIQUEIDENTIFIER NOT NULL,
        [ExternalContentId] NVARCHAR(400) NOT NULL,
        [AclFingerprint] NVARCHAR(64) NULL,
        [IsAvailable] BIT NOT NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL,
        [UpdatedAt] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_Microsoft365IndexedContent] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365IndexedContent_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Microsoft365IndexedContent_Source_Microsoft365SourceId]
            FOREIGN KEY ([Microsoft365SourceId]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_Microsoft365IndexedContent_Identity]
        ON [dbo].[Microsoft365IndexedContent]([OrganizationId], [Microsoft365SourceId], [ExternalContentId]);
END;

IF OBJECT_ID(N'[dbo].[Microsoft365IndexedPassage]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365IndexedPassage]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365IndexedContentId] UNIQUEIDENTIFIER NOT NULL,
        [ChunkId] NVARCHAR(400) NOT NULL,
        CONSTRAINT [PK_Microsoft365IndexedPassage] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365IndexedPassage_Content_Microsoft365IndexedContentId]
            FOREIGN KEY ([Microsoft365IndexedContentId]) REFERENCES [dbo].[Microsoft365IndexedContent]([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_Microsoft365IndexedPassage_ChunkId]
        ON [dbo].[Microsoft365IndexedPassage]([ChunkId]);
    CREATE INDEX [IX_Microsoft365IndexedPassage_Content]
        ON [dbo].[Microsoft365IndexedPassage]([Microsoft365IndexedContentId]);
END;
