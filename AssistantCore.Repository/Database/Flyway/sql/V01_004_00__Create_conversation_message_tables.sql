USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[Conversation]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Conversation]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [OwnerMemberId] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL,
        [UpdatedAt] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_Conversation] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Conversation_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Conversation_OrganizationMember_OwnerMemberId]
            FOREIGN KEY ([OwnerMemberId]) REFERENCES [dbo].[OrganizationMember]([Id]),
        CONSTRAINT [CK_Conversation_Status]
            CHECK ([Status] IN (N'Active', N'Archived'))
    );

    CREATE INDEX [IX_Conversation_OrganizationId_OwnerMemberId_Id]
        ON [dbo].[Conversation]([OrganizationId], [OwnerMemberId], [Id]);
END;

IF OBJECT_ID(N'[dbo].[Message]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Message]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [ConversationId] UNIQUEIDENTIFIER NOT NULL,
        [Role] NVARCHAR(20) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [ProcessingStatus] NVARCHAR(20) NOT NULL,
        [Model] NVARCHAR(100) NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL,
        [UpdatedAt] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_Message] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Message_Conversation_ConversationId]
            FOREIGN KEY ([ConversationId]) REFERENCES [dbo].[Conversation]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_Message_Role]
            CHECK ([Role] IN (N'User', N'Assistant')),
        CONSTRAINT [CK_Message_ProcessingStatus]
            CHECK ([ProcessingStatus] IN (
                N'Pending',
                N'InProgress',
                N'Completed',
                N'Failed',
                N'Cancelled'
            ))
    );

    CREATE INDEX [IX_Message_ConversationId_CreatedAt_Id]
        ON [dbo].[Message]([ConversationId], [CreatedAt], [Id]);
END;

IF OBJECT_ID(N'[dbo].[MessageSource]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MessageSource]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [MessageId] UNIQUEIDENTIFIER NOT NULL,
        [SourceType] NVARCHAR(50) NOT NULL,
        [Title] NVARCHAR(500) NOT NULL,
        [Reference] NVARCHAR(500) NOT NULL,
        [Url] NVARCHAR(2048) NULL,
        [SourceDate] DATETIMEOFFSET NULL,
        CONSTRAINT [PK_MessageSource] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MessageSource_Message_MessageId]
            FOREIGN KEY ([MessageId]) REFERENCES [dbo].[Message]([Id]) ON DELETE CASCADE
    );

    CREATE INDEX [IX_MessageSource_MessageId]
        ON [dbo].[MessageSource]([MessageId]);
END;
