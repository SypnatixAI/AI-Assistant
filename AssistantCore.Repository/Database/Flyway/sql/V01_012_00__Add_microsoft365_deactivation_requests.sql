USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[CK_Microsoft365Subscription_Status]', N'C') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Subscription]
        DROP CONSTRAINT [CK_Microsoft365Subscription_Status];
END;

ALTER TABLE [dbo].[Microsoft365Subscription]
    ADD CONSTRAINT [CK_Microsoft365Subscription_Status]
        CHECK ([Status] IN (
            N'Pending',
            N'Active',
            N'RenewalRequired',
            N'RevocationRequired',
            N'Error',
            N'Revoked',
            N'Expired'));

IF OBJECT_ID(N'[dbo].[CK_Microsoft365Synchronization_Type]', N'C') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Microsoft365Synchronization]
        DROP CONSTRAINT [CK_Microsoft365Synchronization_Type];
END;

ALTER TABLE [dbo].[Microsoft365Synchronization]
    ADD CONSTRAINT [CK_Microsoft365Synchronization_Type]
        CHECK ([Type] IN (N'Initial', N'Delta', N'IndexCleanup'));
