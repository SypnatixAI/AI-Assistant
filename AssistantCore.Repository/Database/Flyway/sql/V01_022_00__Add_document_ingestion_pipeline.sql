USE [AssistantCoreDb];

ALTER TABLE [dbo].[Microsoft365DocumentWork]
ADD [Status] nvarchar(30) NOT NULL CONSTRAINT [DF_Microsoft365DocumentWork_Status] DEFAULT N'Pending',
    [AttemptCount] int NOT NULL CONSTRAINT [DF_Microsoft365DocumentWork_AttemptCount] DEFAULT 0,
    [LeaseId] uniqueidentifier NULL,
    [LeaseExpiresAt] datetimeoffset NULL,
    [NextAttemptAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [LastErrorCode] nvarchar(200) NULL;

CREATE INDEX [IX_Microsoft365DocumentWork_Status_NextAttemptAt_CreatedAt]
ON [dbo].[Microsoft365DocumentWork] ([Status], [NextAttemptAt], [CreatedAt]);

ALTER TABLE [dbo].[Microsoft365IndexedContent]
ADD [DocumentVersion] nvarchar(1000) NULL,
    [Title] nvarchar(1000) NULL,
    [WebUrl] nvarchar(2048) NULL,
    [LastModifiedAt] datetimeoffset NULL;
