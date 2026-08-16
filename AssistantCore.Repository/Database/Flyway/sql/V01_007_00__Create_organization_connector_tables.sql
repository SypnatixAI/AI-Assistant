USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[OrganizationConnector]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationConnector]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Type] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [IsConfigured] BIT NOT NULL,
        CONSTRAINT [PK_OrganizationConnector] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationConnector_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_OrganizationConnector_Type]
            CHECK ([Type] IN (N'Microsoft365', N'Erp', N'Crm', N'InternalData')),
        CONSTRAINT [CK_OrganizationConnector_Status]
            CHECK ([Status] IN (N'Actif', N'Inactif'))
    );

    CREATE UNIQUE INDEX [IX_OrganizationConnector_OrganizationId_Type]
        ON [dbo].[OrganizationConnector]([OrganizationId], [Type]);
END;

IF OBJECT_ID(N'[dbo].[OrganizationConnectorSource]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationConnectorSource]
    (
        [OrganizationConnectorId] UNIQUEIDENTIFIER NOT NULL,
        [SourceType] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [IsIndexed] BIT NOT NULL,
        CONSTRAINT [PK_OrganizationConnectorSource]
            PRIMARY KEY ([OrganizationConnectorId], [SourceType]),
        CONSTRAINT [FK_OrganizationConnectorSource_OrganizationConnector_OrganizationConnectorId]
            FOREIGN KEY ([OrganizationConnectorId])
            REFERENCES [dbo].[OrganizationConnector]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_OrganizationConnectorSource_SourceType]
            CHECK ([SourceType] IN (N'SharePoint', N'OneDrive')),
        CONSTRAINT [CK_OrganizationConnectorSource_Status]
            CHECK ([Status] IN (N'Actif', N'Inactif'))
    );
END;
