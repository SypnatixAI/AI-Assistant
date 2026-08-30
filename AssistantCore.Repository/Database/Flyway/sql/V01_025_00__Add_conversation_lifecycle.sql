USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Conversation]', N'Version') IS NULL
BEGIN
    ALTER TABLE [dbo].[Conversation]
        ADD [Version] INT NOT NULL CONSTRAINT [DF_Conversation_Version] DEFAULT (1);
END;

IF COL_LENGTH(N'[dbo].[Conversation]', N'DeletedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Conversation]
        ADD [DeletedAt] DATETIMEOFFSET NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Conversation_Owner_Visible'
      AND object_id = OBJECT_ID(N'[dbo].[Conversation]')
)
BEGIN
    -- SET explicite : un index filtre exige ANSI_NULLS et QUOTED_IDENTIFIER a ON
    -- au moment du CREATE. Le pilote utilise par Flyway ne garantit pas ces
    -- options, et un defaut different ferait echouer la migration avec l'erreur
    -- 1934 seulement lors du deploiement.
    EXEC(N'
        SET ANSI_NULLS ON;
        SET QUOTED_IDENTIFIER ON;
        CREATE INDEX [IX_Conversation_Owner_Visible]
            ON [dbo].[Conversation]([OrganizationId], [OwnerMemberId], [Status], [UpdatedAt], [Id])
            WHERE [DeletedAt] IS NULL;
    ');
END;

IF OBJECT_ID(N'[dbo].[ConversationPurgeRequest]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ConversationPurgeRequest]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [ConversationId] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [RequestedAt] DATETIMEOFFSET NOT NULL,
        [PurgeAfter] DATETIMEOFFSET NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        CONSTRAINT [PK_ConversationPurgeRequest] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConversationPurgeRequest_Conversation_ConversationId]
            FOREIGN KEY ([ConversationId]) REFERENCES [dbo].[Conversation]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_ConversationPurgeRequest_Status]
            CHECK ([Status] IN (N'Pending', N'Completed'))
    );

    CREATE UNIQUE INDEX [IX_ConversationPurgeRequest_ConversationId]
        ON [dbo].[ConversationPurgeRequest]([ConversationId]);

    CREATE INDEX [IX_ConversationPurgeRequest_Status_PurgeAfter]
        ON [dbo].[ConversationPurgeRequest]([Status], [PurgeAfter]);
END;
