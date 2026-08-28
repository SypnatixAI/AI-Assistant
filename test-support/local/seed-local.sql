USE [AssistantCoreLocalDb];

DECLARE @OrganizationId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000300';
DECLARE @MemberId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000400';
DECLARE @TenantId NVARCHAR(100) = N'00000000-0000-0000-0000-000000000100';
DECLARE @UserId NVARCHAR(100) = N'00000000-0000-0000-0000-000000000200';

IF NOT EXISTS (
    SELECT 1
    FROM [dbo].[Organization]
    WHERE [IdentityProvider] = N'MicrosoftEntraId'
      AND [ExternalTenantId] = @TenantId
)
BEGIN
    INSERT INTO [dbo].[Organization]
        ([Id], [Name], [IdentityProvider], [ExternalTenantId], [Status])
    VALUES
        (@OrganizationId, N'Organisation locale', N'MicrosoftEntraId', @TenantId, N'Actif');
END;

SELECT @OrganizationId = [Id]
FROM [dbo].[Organization]
WHERE [IdentityProvider] = N'MicrosoftEntraId'
  AND [ExternalTenantId] = @TenantId;

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
