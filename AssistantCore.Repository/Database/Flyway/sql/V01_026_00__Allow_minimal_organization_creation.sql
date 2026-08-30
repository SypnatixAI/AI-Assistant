USE [AssistantCoreDb];

IF COL_LENGTH(N'[dbo].[Organization]', N'Domain') IS NULL
BEGIN
    ALTER TABLE [dbo].[Organization]
        ADD [Domain] NVARCHAR(200) NULL;
END;

EXEC(N'
    UPDATE [dbo].[Organization]
    SET [Domain] = LOWER(LTRIM(RTRIM([Name])))
    WHERE [Domain] IS NULL OR LTRIM(RTRIM([Domain])) = N'''';
');

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Organization]')
      AND name = N'Domain'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE [dbo].[Organization]
        ALTER COLUMN [Domain] NVARCHAR(200) NOT NULL;
END;

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Organization]')
      AND name = N'ExternalTenantId'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE [dbo].[Organization]
        ALTER COLUMN [ExternalTenantId] NVARCHAR(100) NULL;
END;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Organization_IdentityProvider_ExternalTenantId'
      AND object_id = OBJECT_ID(N'[dbo].[Organization]')
)
BEGIN
    DROP INDEX [IX_Organization_IdentityProvider_ExternalTenantId] ON [dbo].[Organization];
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Organization_IdentityProvider_Domain'
      AND object_id = OBJECT_ID(N'[dbo].[Organization]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_Organization_IdentityProvider_Domain]
        ON [dbo].[Organization]([IdentityProvider], [Domain]);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Organization_IdentityProvider_ExternalTenantId'
      AND object_id = OBJECT_ID(N'[dbo].[Organization]')
)
BEGIN
    EXEC(N'
        CREATE UNIQUE INDEX [IX_Organization_IdentityProvider_ExternalTenantId]
            ON [dbo].[Organization]([IdentityProvider], [ExternalTenantId])
            WHERE [ExternalTenantId] IS NOT NULL;
    ');
END;
