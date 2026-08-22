USE [AssistantCoreDb];

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Microsoft365Subscription_OneActivePerSource'
      AND [object_id] = OBJECT_ID(N'[dbo].[Microsoft365Subscription]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Microsoft365Subscription_OneActivePerSource]
        ON [dbo].[Microsoft365Subscription]([Microsoft365SourceId])
        WHERE [Status] = N'Active';
END;
