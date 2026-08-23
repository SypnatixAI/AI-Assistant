USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Microsoft365Source]', N'LastSynchronizationAttemptAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Source]
        ADD [LastSynchronizationAttemptAt] DATETIMEOFFSET NULL;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365Source]', N'SynchronizationLeaseId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Source]
        ADD [SynchronizationLeaseId] UNIQUEIDENTIFIER NULL;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365Source]', N'SynchronizationLeaseExpiresAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Source]
        ADD [SynchronizationLeaseExpiresAt] DATETIMEOFFSET NULL;
END;

ALTER TABLE [dbo].[Microsoft365Source]
    ALTER COLUMN [DeltaLink] NVARCHAR(MAX) NULL;

IF OBJECT_ID(N'[dbo].[CK_Microsoft365Source_SynchronizationLease]', N'C') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Source]
        ADD CONSTRAINT [CK_Microsoft365Source_SynchronizationLease]
            CHECK (
                ([SynchronizationLeaseId] IS NULL
                    AND [SynchronizationLeaseExpiresAt] IS NULL)
                OR
                ([SynchronizationLeaseId] IS NOT NULL
                    AND [SynchronizationLeaseExpiresAt] IS NOT NULL)
            );
END;
