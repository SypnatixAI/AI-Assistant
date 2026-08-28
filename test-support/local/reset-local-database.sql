USE [master];

IF DB_ID(N'AssistantCoreLocalDb') IS NOT NULL
BEGIN
    ALTER DATABASE [AssistantCoreLocalDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AssistantCoreLocalDb];
END;
