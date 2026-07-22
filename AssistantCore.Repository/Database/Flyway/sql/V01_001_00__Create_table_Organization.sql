USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[Organization]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Organization]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [MicrosoftTenantId] NVARCHAR(100) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        CONSTRAINT [PK_Organization] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Organization_Status] CHECK ([Status] IN (N'Actif', N'Inactif'))
    );
END;
