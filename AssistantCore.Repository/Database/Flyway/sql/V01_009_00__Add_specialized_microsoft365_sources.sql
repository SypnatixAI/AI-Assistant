USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[Microsoft365Site]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365Site]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationConnectorId] UNIQUEIDENTIFIER NOT NULL,
        [SiteId] NVARCHAR(400) NOT NULL,
        CONSTRAINT [PK_Microsoft365Site] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365Site_Microsoft365Source_Id]
            FOREIGN KEY ([Id]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Microsoft365Site_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Microsoft365Site_OrganizationConnector_OrganizationConnectorId]
            FOREIGN KEY ([OrganizationConnectorId]) REFERENCES [dbo].[OrganizationConnector]([Id])
    );

    CREATE UNIQUE INDEX [IX_Microsoft365Site_Organization_Connector_Site]
        ON [dbo].[Microsoft365Site]([OrganizationId], [OrganizationConnectorId], [SiteId]);

    INSERT INTO [dbo].[Microsoft365Site]
        ([Id], [OrganizationId], [OrganizationConnectorId], [SiteId])
    SELECT source.[Id], connection.[OrganizationId], connection.[OrganizationConnectorId], source.[ExternalResourceId]
    FROM [dbo].[Microsoft365Source] source
    INNER JOIN [dbo].[Microsoft365Connection] connection
        ON connection.[Id] = source.[Microsoft365ConnectionId]
    WHERE source.[Kind] = N'SharePointSite';
END;

IF OBJECT_ID(N'[dbo].[Microsoft365Drive]', N'U') IS NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM [dbo].[Microsoft365Source]
        WHERE [Kind] = N'SharePointDrive'
          AND [ParentExternalResourceId] IS NULL
    )
        THROW 51000, 'A SharePoint drive source must identify its parent site.', 1;

    CREATE TABLE [dbo].[Microsoft365Drive]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationConnectorId] UNIQUEIDENTIFIER NOT NULL,
        [SiteId] NVARCHAR(400) NOT NULL,
        [DriveId] NVARCHAR(400) NOT NULL,
        CONSTRAINT [PK_Microsoft365Drive] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365Drive_Microsoft365Source_Id]
            FOREIGN KEY ([Id]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Microsoft365Drive_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Microsoft365Drive_OrganizationConnector_OrganizationConnectorId]
            FOREIGN KEY ([OrganizationConnectorId]) REFERENCES [dbo].[OrganizationConnector]([Id])
    );

    CREATE UNIQUE INDEX [IX_Microsoft365Drive_Organization_Connector_Site_Drive]
        ON [dbo].[Microsoft365Drive]([OrganizationId], [OrganizationConnectorId], [SiteId], [DriveId]);

    INSERT INTO [dbo].[Microsoft365Drive]
        ([Id], [OrganizationId], [OrganizationConnectorId], [SiteId], [DriveId])
    SELECT source.[Id], connection.[OrganizationId], connection.[OrganizationConnectorId],
           source.[ParentExternalResourceId], source.[ExternalResourceId]
    FROM [dbo].[Microsoft365Source] source
    INNER JOIN [dbo].[Microsoft365Connection] connection
        ON connection.[Id] = source.[Microsoft365ConnectionId]
    WHERE source.[Kind] = N'SharePointDrive';
END;

IF OBJECT_ID(N'[dbo].[Microsoft365List]', N'U') IS NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM [dbo].[Microsoft365Source]
        WHERE [Kind] = N'SharePointList'
          AND [ParentExternalResourceId] IS NULL
    )
        THROW 51001, 'A SharePoint list source must identify its parent site.', 1;

    CREATE TABLE [dbo].[Microsoft365List]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationConnectorId] UNIQUEIDENTIFIER NOT NULL,
        [SiteId] NVARCHAR(400) NOT NULL,
        [ListId] NVARCHAR(400) NOT NULL,
        CONSTRAINT [PK_Microsoft365List] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365List_Microsoft365Source_Id]
            FOREIGN KEY ([Id]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Microsoft365List_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Microsoft365List_OrganizationConnector_OrganizationConnectorId]
            FOREIGN KEY ([OrganizationConnectorId]) REFERENCES [dbo].[OrganizationConnector]([Id])
    );

    CREATE UNIQUE INDEX [IX_Microsoft365List_Organization_Connector_Site_List]
        ON [dbo].[Microsoft365List]([OrganizationId], [OrganizationConnectorId], [SiteId], [ListId]);

    INSERT INTO [dbo].[Microsoft365List]
        ([Id], [OrganizationId], [OrganizationConnectorId], [SiteId], [ListId])
    SELECT source.[Id], connection.[OrganizationId], connection.[OrganizationConnectorId],
           source.[ParentExternalResourceId], source.[ExternalResourceId]
    FROM [dbo].[Microsoft365Source] source
    INNER JOIN [dbo].[Microsoft365Connection] connection
        ON connection.[Id] = source.[Microsoft365ConnectionId]
    WHERE source.[Kind] = N'SharePointList';
END;
