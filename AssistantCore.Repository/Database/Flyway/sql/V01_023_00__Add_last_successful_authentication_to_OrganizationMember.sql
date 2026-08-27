USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[OrganizationMember]', N'LastSuccessfulAuthenticationAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationMember]
        ADD [LastSuccessfulAuthenticationAt] DATETIMEOFFSET NULL;
END;
