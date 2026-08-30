USE [AssistantCoreLocalDb];
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
GO

DECLARE @OrganizationId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000300';
DECLARE @MemberId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000400';
DECLARE @TenantId NVARCHAR(100) = N'00000000-0000-0000-0000-000000000100';
DECLARE @UserId NVARCHAR(100) = N'00000000-0000-0000-0000-000000000200';
DECLARE @Domain NVARCHAR(200) = N'local.test';

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[Organization]
    WHERE [IdentityProvider] = N'MicrosoftEntraId'
      AND (
          [ExternalTenantId] = @TenantId
          OR (COL_LENGTH(N'[dbo].[Organization]', N'Domain') IS NOT NULL AND [Domain] = @Domain)
      )
)
BEGIN
    IF COL_LENGTH(N'[dbo].[Organization]', N'Domain') IS NULL
    BEGIN
        INSERT INTO [dbo].[Organization]
            ([Id], [Name], [IdentityProvider], [ExternalTenantId], [Status])
        VALUES
            (@OrganizationId, N'Organisation locale', N'MicrosoftEntraId', @TenantId, N'Actif');
    END
    ELSE
    BEGIN
        INSERT INTO [dbo].[Organization]
            ([Id], [Name], [Domain], [IdentityProvider], [ExternalTenantId], [Status])
        VALUES
            (@OrganizationId, N'Organisation locale', @Domain, N'MicrosoftEntraId', @TenantId, N'Actif');
    END
END;

IF COL_LENGTH(N'[dbo].[Organization]', N'Domain') IS NOT NULL
BEGIN
    UPDATE [dbo].[Organization]
    SET [Domain] = @Domain,
        [ExternalTenantId] = COALESCE([ExternalTenantId], @TenantId)
    WHERE [IdentityProvider] = N'MicrosoftEntraId'
      AND (
          [ExternalTenantId] = @TenantId
          OR [Domain] = @Domain
      );
END;

SELECT TOP (1) @OrganizationId = [Id]
FROM [dbo].[Organization]
WHERE [IdentityProvider] = N'MicrosoftEntraId'
  AND (
      [ExternalTenantId] = @TenantId
      OR (COL_LENGTH(N'[dbo].[Organization]', N'Domain') IS NOT NULL AND [Domain] = @Domain)
  );

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[OrganizationMember]
    WHERE [OrganizationId] = @OrganizationId
      AND [IdentityProvider] = N'MicrosoftEntraId'
      AND [ExternalUserId] = @UserId
)
BEGIN
    INSERT INTO [dbo].[OrganizationMember]
        ([Id], [OrganizationId], [Name], [Email], [IdentityProvider], [ExternalUserId], [Role], [Status])
    VALUES
        (@MemberId, @OrganizationId, N'Administrateur local', N'admin@local.test', N'MicrosoftEntraId', @UserId, N'Admin', N'Actif');
END;
