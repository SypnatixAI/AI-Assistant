USE [AssistantCoreDb];

IF OBJECT_ID(N'[dbo].[Microsoft365Connection]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365Connection]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationConnectorId] UNIQUEIDENTIFIER NOT NULL,
        [TenantId] NVARCHAR(100) NULL,
        [Status] NVARCHAR(30) NOT NULL,
        [ConsentStateHash] NVARCHAR(64) NULL,
        [ConsentStateExpiresAt] DATETIMEOFFSET NULL,
        [ConsentStateConsumedAt] DATETIMEOFFSET NULL,
        [ConsentValidatedAt] DATETIMEOFFSET NULL,
        [LastErrorCode] NVARCHAR(100) NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL,
        [UpdatedAt] DATETIMEOFFSET NOT NULL,
        [RowVersion] ROWVERSION NOT NULL,
        CONSTRAINT [PK_Microsoft365Connection] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365Connection_Organization_OrganizationId]
            FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]),
        CONSTRAINT [FK_Microsoft365Connection_OrganizationConnector_OrganizationConnectorId]
            FOREIGN KEY ([OrganizationConnectorId]) REFERENCES [dbo].[OrganizationConnector]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_Microsoft365Connection_Status]
            CHECK ([Status] IN (N'PendingConsent', N'Active', N'Error', N'Revoked'))
    );

    CREATE UNIQUE INDEX [IX_Microsoft365Connection_OrganizationId]
        ON [dbo].[Microsoft365Connection]([OrganizationId]);
    CREATE UNIQUE INDEX [IX_Microsoft365Connection_OrganizationConnectorId]
        ON [dbo].[Microsoft365Connection]([OrganizationConnectorId]);
    CREATE UNIQUE INDEX [IX_Microsoft365Connection_TenantId]
        ON [dbo].[Microsoft365Connection]([TenantId]) WHERE [TenantId] IS NOT NULL;
    CREATE UNIQUE INDEX [IX_Microsoft365Connection_ConsentStateHash]
        ON [dbo].[Microsoft365Connection]([ConsentStateHash]) WHERE [ConsentStateHash] IS NOT NULL;
END;

IF OBJECT_ID(N'[dbo].[Microsoft365Source]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365Source]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365ConnectionId] UNIQUEIDENTIFIER NOT NULL,
        [Kind] NVARCHAR(50) NOT NULL,
        [ExternalResourceId] NVARCHAR(400) NOT NULL,
        [ParentExternalResourceId] NVARCHAR(400) NULL,
        [DisplayName] NVARCHAR(300) NOT NULL,
        [WebUrl] NVARCHAR(2048) NULL,
        [Status] NVARCHAR(30) NOT NULL,
        [IsIndexed] BIT NOT NULL,
        [DeltaLink] NVARCHAR(4000) NULL,
        [DiscoveredAt] DATETIMEOFFSET NOT NULL,
        [EnabledAt] DATETIMEOFFSET NULL,
        [LastSuccessfulSynchronizationAt] DATETIMEOFFSET NULL,
        [NextSynchronizationAt] DATETIMEOFFSET NULL,
        [LastErrorCode] NVARCHAR(100) NULL,
        CONSTRAINT [PK_Microsoft365Source] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365Source_Microsoft365Connection_Microsoft365ConnectionId]
            FOREIGN KEY ([Microsoft365ConnectionId]) REFERENCES [dbo].[Microsoft365Connection]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_Microsoft365Source_Kind]
            CHECK ([Kind] IN (N'SharePointSite', N'SharePointDrive', N'SharePointList', N'OneDrive')),
        CONSTRAINT [CK_Microsoft365Source_Status]
            CHECK ([Status] IN (N'Discovered', N'Enabled', N'Disabled', N'Error'))
    );

    CREATE UNIQUE INDEX [IX_Microsoft365Source_Connection_Type_Resource]
        ON [dbo].[Microsoft365Source]([Microsoft365ConnectionId], [Kind], [ExternalResourceId]);
END;

IF OBJECT_ID(N'[dbo].[Microsoft365Subscription]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365Subscription]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365SourceId] UNIQUEIDENTIFIER NOT NULL,
        [MicrosoftSubscriptionId] NVARCHAR(150) NOT NULL,
        [ProtectedClientState] NVARCHAR(2048) NOT NULL,
        [ExpiresAt] DATETIMEOFFSET NOT NULL,
        [LastRenewedAt] DATETIMEOFFSET NULL,
        [Status] NVARCHAR(30) NOT NULL,
        [LastErrorCode] NVARCHAR(100) NULL,
        [CreatedAt] DATETIMEOFFSET NOT NULL,
        [UpdatedAt] DATETIMEOFFSET NOT NULL,
        CONSTRAINT [PK_Microsoft365Subscription] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365Subscription_Microsoft365Source_Microsoft365SourceId]
            FOREIGN KEY ([Microsoft365SourceId]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_Microsoft365Subscription_Status]
            CHECK ([Status] IN (N'Pending', N'Active', N'RenewalRequired', N'Error', N'Revoked', N'Expired'))
    );

    CREATE UNIQUE INDEX [IX_Microsoft365Subscription_MicrosoftSubscriptionId]
        ON [dbo].[Microsoft365Subscription]([MicrosoftSubscriptionId]);
    CREATE INDEX [IX_Microsoft365Subscription_Source_Status]
        ON [dbo].[Microsoft365Subscription]([Microsoft365SourceId], [Status]);
END;

IF OBJECT_ID(N'[dbo].[Microsoft365Synchronization]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Microsoft365Synchronization]
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Microsoft365SourceId] UNIQUEIDENTIFIER NOT NULL,
        [Type] NVARCHAR(20) NOT NULL,
        [Status] NVARCHAR(30) NOT NULL,
        [AttemptCount] INT NOT NULL,
        [RequestedAt] DATETIMEOFFSET NOT NULL,
        [StartedAt] DATETIMEOFFSET NULL,
        [CompletedAt] DATETIMEOFFSET NULL,
        [LastErrorCode] NVARCHAR(100) NULL,
        CONSTRAINT [PK_Microsoft365Synchronization] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Microsoft365Synchronization_Microsoft365Source_Microsoft365SourceId]
            FOREIGN KEY ([Microsoft365SourceId]) REFERENCES [dbo].[Microsoft365Source]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_Microsoft365Synchronization_Type]
            CHECK ([Type] IN (N'Initial', N'Delta')),
        CONSTRAINT [CK_Microsoft365Synchronization_Status]
            CHECK ([Status] IN (N'Pending', N'Running', N'Succeeded', N'TemporaryFailure', N'PermanentFailure', N'Cancelled')),
        CONSTRAINT [CK_Microsoft365Synchronization_AttemptCount]
            CHECK ([AttemptCount] >= 0)
    );

    CREATE INDEX [IX_Microsoft365Synchronization_Source_Status]
        ON [dbo].[Microsoft365Synchronization]([Microsoft365SourceId], [Status]);
    CREATE UNIQUE INDEX [IX_Microsoft365Synchronization_OneRunningPerSource]
        ON [dbo].[Microsoft365Synchronization]([Microsoft365SourceId]) WHERE [Status] = N'Running';
END;
