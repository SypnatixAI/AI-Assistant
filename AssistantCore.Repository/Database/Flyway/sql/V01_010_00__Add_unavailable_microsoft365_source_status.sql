USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Microsoft365Source]', N'StatusBeforeUnavailable') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Source]
        ADD [StatusBeforeUnavailable] NVARCHAR(30) NULL;
END;

GO

IF OBJECT_ID(N'[dbo].[CK_Microsoft365Source_Status]', N'C') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Source]
        DROP CONSTRAINT [CK_Microsoft365Source_Status];
END;

ALTER TABLE [dbo].[Microsoft365Source]
    ADD CONSTRAINT [CK_Microsoft365Source_Status]
        CHECK ([Status] IN (
            N'Discovered',
            N'Enabled',
            N'Disabled',
            N'Error',
            N'Unavailable',
            N'FullResyncRequired'
        ));

ALTER TABLE [dbo].[Microsoft365Source]
    ADD CONSTRAINT [CK_Microsoft365Source_StatusBeforeUnavailable]
        CHECK ([StatusBeforeUnavailable] IS NULL
            OR [StatusBeforeUnavailable] IN (N'Discovered', N'Enabled', N'Disabled', N'Error'));
