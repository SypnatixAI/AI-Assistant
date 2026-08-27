USE [AssistantCoreDb];

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Conversation_OrganizationId_OwnerMemberId_Status_UpdatedAt_Id'
      AND object_id = OBJECT_ID(N'[dbo].[Conversation]')
)
BEGIN
    CREATE INDEX [IX_Conversation_OrganizationId_OwnerMemberId_Status_UpdatedAt_Id]
        ON [dbo].[Conversation]([OrganizationId], [OwnerMemberId], [Status], [UpdatedAt], [Id]);
END;
