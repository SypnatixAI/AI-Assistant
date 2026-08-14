USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Message]', N'ProcessingErrorCode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Message]
        ADD [ProcessingErrorCode] NVARCHAR(100) NULL;
END;
