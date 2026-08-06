USE [AssistantCoreDb];

IF EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_OrganizationMember_Role'
      AND parent_object_id = OBJECT_ID(N'[dbo].[OrganizationMember]')
)
BEGIN
    ALTER TABLE [dbo].[OrganizationMember]
    DROP CONSTRAINT [CK_OrganizationMember_Role];
END;

UPDATE [dbo].[OrganizationMember]
SET [Role] = N'Admin'
WHERE [Role] IN (N'TenantAdmin', N'Manager');

ALTER TABLE [dbo].[OrganizationMember]
ADD CONSTRAINT [CK_OrganizationMember_Role]
CHECK ([Role] IN (N'Admin', N'User'));
