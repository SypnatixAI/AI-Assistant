USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Organization]', N'CreatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Organization]
        ADD [CreatedAt] DATETIMEOFFSET NOT NULL
            CONSTRAINT [DF_Organization_CreatedAt] DEFAULT SYSUTCDATETIME();
END;
