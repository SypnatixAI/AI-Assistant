USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Microsoft365Subscription]', N'OrganizationId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Subscription]
        ADD [OrganizationId] UNIQUEIDENTIFIER NULL;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365Subscription]', N'Resource') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Subscription]
        ADD [Resource] NVARCHAR(1000) NULL;
END;

GO

UPDATE subscription
SET
    [OrganizationId] = connection.[OrganizationId],
    [Resource] = CASE
        WHEN source.[Kind] = N'SharePointList'
            THEN N'/sites/' + source.[ParentExternalResourceId] + N'/lists/' + source.[ExternalResourceId]
        WHEN source.[Kind] IN (N'SharePointDrive', N'OneDrive')
            THEN N'/drives/' + source.[ExternalResourceId] + N'/root'
        ELSE N'/sites/' + source.[ExternalResourceId]
    END
FROM [dbo].[Microsoft365Subscription] subscription
INNER JOIN [dbo].[Microsoft365Source] source
    ON source.[Id] = subscription.[Microsoft365SourceId]
INNER JOIN [dbo].[Microsoft365Connection] connection
    ON connection.[Id] = source.[Microsoft365ConnectionId]
WHERE subscription.[OrganizationId] IS NULL
   OR subscription.[Resource] IS NULL;

ALTER TABLE [dbo].[Microsoft365Subscription]
    ALTER COLUMN [OrganizationId] UNIQUEIDENTIFIER NOT NULL;

ALTER TABLE [dbo].[Microsoft365Subscription]
    ALTER COLUMN [Resource] NVARCHAR(1000) NOT NULL;

ALTER TABLE [dbo].[Microsoft365Subscription]
    ALTER COLUMN [MicrosoftSubscriptionId] NVARCHAR(150) NULL;

ALTER TABLE [dbo].[Microsoft365Subscription]
    ALTER COLUMN [ProtectedClientState] NVARCHAR(2048) NULL;

ALTER TABLE [dbo].[Microsoft365Subscription]
    ALTER COLUMN [ExpiresAt] DATETIMEOFFSET NULL;

IF OBJECT_ID(N'[dbo].[FK_Microsoft365Subscription_Organization_OrganizationId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Subscription]
        ADD CONSTRAINT [FK_Microsoft365Subscription_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]);
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Microsoft365Subscription_MicrosoftSubscriptionId'
      AND [object_id] = OBJECT_ID(N'[dbo].[Microsoft365Subscription]'))
BEGIN
    DROP INDEX [IX_Microsoft365Subscription_MicrosoftSubscriptionId]
        ON [dbo].[Microsoft365Subscription];
END;

CREATE UNIQUE INDEX [IX_Microsoft365Subscription_MicrosoftSubscriptionId]
    ON [dbo].[Microsoft365Subscription]([MicrosoftSubscriptionId])
    WHERE [MicrosoftSubscriptionId] IS NOT NULL;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Microsoft365Subscription_OrganizationId'
      AND [object_id] = OBJECT_ID(N'[dbo].[Microsoft365Subscription]'))
BEGIN
    CREATE INDEX [IX_Microsoft365Subscription_OrganizationId]
        ON [dbo].[Microsoft365Subscription]([OrganizationId]);
END;
