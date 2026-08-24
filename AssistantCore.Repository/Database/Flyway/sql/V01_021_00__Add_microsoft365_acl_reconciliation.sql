USE [AssistantCoreDb];

IF COL_LENGTH(N'dbo.Microsoft365IndexedContent', N'SiteUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365IndexedContent]
        ADD [SiteUrl] NVARCHAR(2048) NULL;
END;

IF COL_LENGTH(N'dbo.Microsoft365IndexedContent', N'NextAclReconciliationAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365IndexedContent]
        ADD [NextAclReconciliationAt] DATETIMEOFFSET NULL;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Microsoft365IndexedContent_NextAclReconciliationAt'
      AND [object_id] = OBJECT_ID(N'[dbo].[Microsoft365IndexedContent]'))
BEGIN
    CREATE INDEX [IX_Microsoft365IndexedContent_NextAclReconciliationAt]
        ON [dbo].[Microsoft365IndexedContent]([NextAclReconciliationAt]);
END;
