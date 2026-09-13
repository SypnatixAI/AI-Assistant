USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[OrganizationMember]', N'Version') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationMember]
        ADD [Version] INT NOT NULL CONSTRAINT [DF_OrganizationMember_Version] DEFAULT (1);
END;
