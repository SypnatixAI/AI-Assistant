USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[OrganizationMember]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationMember]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Email] NVARCHAR(320) NOT NULL,
        [MicrosoftIdentifier] NVARCHAR(100) NOT NULL,
        [Role] NVARCHAR(100) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        CONSTRAINT [PK_OrganizationMember] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationMember_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_OrganizationMember_Status] CHECK ([Status] IN (N'Actif', N'Inactif'))
    );

    CREATE UNIQUE INDEX [IX_OrganizationMember_OrganizationId_Email]
        ON [dbo].[OrganizationMember]([OrganizationId], [Email]);
END;
