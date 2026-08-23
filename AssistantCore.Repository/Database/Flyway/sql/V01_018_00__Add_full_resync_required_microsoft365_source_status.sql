USE [AssistantCoreDb];

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
