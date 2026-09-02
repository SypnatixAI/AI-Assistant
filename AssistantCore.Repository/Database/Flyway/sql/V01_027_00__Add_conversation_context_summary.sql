USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Conversation]', N'ContextSummary') IS NULL
BEGIN
    ALTER TABLE [dbo].[Conversation]
        ADD [ContextSummary] NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'[dbo].[Conversation]', N'ContextSummaryUpdatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Conversation]
        ADD [ContextSummaryUpdatedAt] DATETIMEOFFSET NULL;
END;
