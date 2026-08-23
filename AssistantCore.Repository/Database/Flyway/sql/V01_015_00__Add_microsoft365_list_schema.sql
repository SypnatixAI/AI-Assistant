USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Microsoft365List]', N'SchemaFingerprint') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365List]
        ADD [SchemaFingerprint] NVARCHAR(64) NULL;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365List]', N'RequiresItemReprocessing') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365List]
        ADD [RequiresItemReprocessing] BIT NOT NULL
            CONSTRAINT [DF_Microsoft365List_RequiresItemReprocessing] DEFAULT 0;
END;
