USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[Microsoft365DocumentWork]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365DocumentWork]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365SourceId] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365SynchronizationId] UNIQUEIDENTIFIER NOT NULL,
        [WorkType] NVARCHAR(30) NOT NULL,
        [SiteId] NVARCHAR(400) NOT NULL,
        [DriveId] NVARCHAR(400) NOT NULL,
        [DriveItemId] NVARCHAR(400) NOT NULL,
        [Name] NVARCHAR(1000) NULL,
        [ETag] NVARCHAR(1000) NULL,
        [CreatedDateTime] DATETIMEOFFSET NULL,
        [LastModifiedDateTime] DATETIMEOFFSET NULL,
        [WebUrl] NVARCHAR(2048) NULL,
        [Size] BIGINT NULL,
        [MimeType] NVARCHAR(300) NULL,
        [DeduplicationKey] NVARCHAR(64) NOT NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_Microsoft365DocumentWork] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365DocumentWork_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Microsoft365DocumentWork_Source_Microsoft365SourceId]
            FOREIGN KEY ([Microsoft365SourceId]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Microsoft365DocumentWork_Synchronization_Microsoft365SynchronizationId]
            FOREIGN KEY ([Microsoft365SynchronizationId]) REFERENCES [dbo].[Microsoft365Synchronization]([Id]),
        CONSTRAINT [CK_Microsoft365DocumentWork_WorkType]
            CHECK ([WorkType] IN (N'ProcessDocument', N'DeleteDocument')),
        CONSTRAINT [CK_Microsoft365DocumentWork_Payload]
            CHECK (
                ([WorkType] = N'ProcessDocument' AND [Name] IS NOT NULL AND [ETag] IS NOT NULL)
                OR
                ([WorkType] = N'DeleteDocument' AND [Name] IS NULL AND [ETag] IS NULL AND [Size] IS NULL AND [MimeType] IS NULL)
            )
    );

    CREATE UNIQUE INDEX [IX_Microsoft365DocumentWork_DeduplicationKey]
        ON [dbo].[Microsoft365DocumentWork]([DeduplicationKey]);
    CREATE INDEX [IX_Microsoft365DocumentWork_Source_CreatedAt]
        ON [dbo].[Microsoft365DocumentWork]([Microsoft365SourceId], [CreatedAt]);
END;
