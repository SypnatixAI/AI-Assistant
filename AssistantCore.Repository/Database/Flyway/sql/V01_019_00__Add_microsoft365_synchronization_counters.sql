USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Microsoft365Synchronization]', N'CreatedCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Synchronization]
        ADD [CreatedCount] INT NOT NULL CONSTRAINT [DF_Microsoft365Synchronization_CreatedCount] DEFAULT 0;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365Synchronization]', N'ModifiedCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Synchronization]
        ADD [ModifiedCount] INT NOT NULL CONSTRAINT [DF_Microsoft365Synchronization_ModifiedCount] DEFAULT 0;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365Synchronization]', N'DeletedCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Synchronization]
        ADD [DeletedCount] INT NOT NULL CONSTRAINT [DF_Microsoft365Synchronization_DeletedCount] DEFAULT 0;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365Synchronization]', N'IgnoredCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Synchronization]
        ADD [IgnoredCount] INT NOT NULL CONSTRAINT [DF_Microsoft365Synchronization_IgnoredCount] DEFAULT 0;
END;

IF COL_LENGTH(N'[dbo].[Microsoft365Synchronization]', N'FailedCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Synchronization]
        ADD [FailedCount] INT NOT NULL CONSTRAINT [DF_Microsoft365Synchronization_FailedCount] DEFAULT 0;
END;

IF OBJECT_ID(N'[dbo].[CK_Microsoft365Synchronization_Counters]', N'C') IS NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Synchronization]
        ADD CONSTRAINT [CK_Microsoft365Synchronization_Counters]
            CHECK (
                [CreatedCount] >= 0
                AND [ModifiedCount] >= 0
                AND [DeletedCount] >= 0
                AND [IgnoredCount] >= 0
                AND [FailedCount] >= 0
            );
END;
