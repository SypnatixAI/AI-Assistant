USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[Microsoft365ListItemWork]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365ListItemWork]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365SourceId] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365SynchronizationId] UNIQUEIDENTIFIER NOT NULL,
        [WorkType] NVARCHAR(30) NOT NULL,
        [SiteId] NVARCHAR(400) NOT NULL,
        [ListId] NVARCHAR(400) NOT NULL,
        [ListItemId] NVARCHAR(400) NOT NULL,
        [ETag] NVARCHAR(1000) NULL,
        [CreatedDateTime] DATETIMEOFFSET NULL,
        [LastModifiedDateTime] DATETIMEOFFSET NULL,
        [WebUrl] NVARCHAR(2048) NULL,
        [FieldsJson] NVARCHAR(MAX) NULL,
        [DeduplicationKey] NVARCHAR(64) NOT NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_Microsoft365ListItemWork] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365ListItemWork_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Microsoft365ListItemWork_Source_Microsoft365SourceId]
            FOREIGN KEY ([Microsoft365SourceId]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Microsoft365ListItemWork_Synchronization_Microsoft365SynchronizationId]
            FOREIGN KEY ([Microsoft365SynchronizationId]) REFERENCES [dbo].[Microsoft365Synchronization]([Id]),
        CONSTRAINT [CK_Microsoft365ListItemWork_WorkType]
            CHECK ([WorkType] IN (N'ProcessListItem', N'DeleteListItem')),
        CONSTRAINT [CK_Microsoft365ListItemWork_Payload]
            CHECK (
                ([WorkType] = N'ProcessListItem' AND [ETag] IS NOT NULL AND [FieldsJson] IS NOT NULL)
                OR
                ([WorkType] = N'DeleteListItem' AND [ETag] IS NULL AND [FieldsJson] IS NULL)
            )
    );

    CREATE UNIQUE INDEX [IX_Microsoft365ListItemWork_DeduplicationKey]
        ON [dbo].[Microsoft365ListItemWork]([DeduplicationKey]);
    CREATE INDEX [IX_Microsoft365ListItemWork_Source_CreatedAt]
        ON [dbo].[Microsoft365ListItemWork]([Microsoft365SourceId], [CreatedAt]);
END;
