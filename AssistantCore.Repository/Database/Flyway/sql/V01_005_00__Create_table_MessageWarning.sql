USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[MessageWarning]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MessageWarning]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [MessageId] UNIQUEIDENTIFIER NOT NULL,
        [Content] NVARCHAR(1000) NOT NULL,
        CONSTRAINT [PK_MessageWarning] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessageWarning_Message_MessageId]
            FOREIGN KEY ([MessageId]) REFERENCES [dbo].[Message]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_MessageWarning_MessageId]
        ON [dbo].[MessageWarning]([MessageId]);
END;
