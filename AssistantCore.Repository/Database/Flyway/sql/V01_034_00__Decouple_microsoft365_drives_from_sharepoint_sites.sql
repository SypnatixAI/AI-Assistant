USE [AssistantCoreDb];

IF COL_LENGTH(N'dbo.Microsoft365Drive', N'OwnerUserObjectId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Drive]
        ADD [OwnerUserObjectId] NVARCHAR(400) NULL;
END;

IF COL_LENGTH(N'dbo.Microsoft365Drive', N'OwnerUserPrincipalName') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Drive]
        ADD [OwnerUserPrincipalName] NVARCHAR(320) NULL;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Microsoft365Drive_Organization_Connector_Site_Drive'
      AND [object_id] = OBJECT_ID(N'[dbo].[Microsoft365Drive]')
)
BEGIN
    DROP INDEX [IX_Microsoft365Drive_Organization_Connector_Site_Drive]
        ON [dbo].[Microsoft365Drive];
END;

ALTER TABLE [dbo].[Microsoft365Drive]
    ALTER COLUMN [SiteId] NVARCHAR(400) NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Microsoft365Drive_Organization_Drive'
      AND [object_id] = OBJECT_ID(N'[dbo].[Microsoft365Drive]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_Microsoft365Drive_Organization_Drive]
        ON [dbo].[Microsoft365Drive]([OrganizationId], [DriveId]);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Microsoft365Drive_Organization_Owner'
      AND [object_id] = OBJECT_ID(N'[dbo].[Microsoft365Drive]')
)
BEGIN
    CREATE INDEX [IX_Microsoft365Drive_Organization_Owner]
        ON [dbo].[Microsoft365Drive]([OrganizationId], [OwnerUserObjectId]);
END;
