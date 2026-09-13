USE [AssistantCoreDb];

CREATE TABLE [dbo].[AdministrativeAuditEntry] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [ActorType] NVARCHAR(50) NOT NULL,
    [ActorId] UNIQUEIDENTIFIER NOT NULL,
    [Action] NVARCHAR(50) NOT NULL,
    [TargetType] NVARCHAR(50) NOT NULL,
    [TargetId] UNIQUEIDENTIFIER NOT NULL,
    [OccurredAt] DATETIMEOFFSET NOT NULL,
    [OldValues] NVARCHAR(MAX) NOT NULL,
    [NewValues] NVARCHAR(MAX) NOT NULL,
    [CorrelationId] NVARCHAR(100) NOT NULL,
    CONSTRAINT [PK_AdministrativeAuditEntry] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AdministrativeAuditEntry_Organization] FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organization] ([Id])
);

CREATE INDEX [IX_AdministrativeAuditEntry_OrganizationId_OccurredAt]
    ON [dbo].[AdministrativeAuditEntry] ([OrganizationId], [OccurredAt]);

CREATE INDEX [IX_AdministrativeAuditEntry_TargetType_TargetId]
    ON [dbo].[AdministrativeAuditEntry] ([TargetType], [TargetId]);
