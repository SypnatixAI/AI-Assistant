USE [AssistantCoreDb];

CREATE TABLE [dbo].[OrganizationUsagePolicy] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [Version] INT NOT NULL,
    [MonthlyTokenLimit] BIGINT NOT NULL,
    [Status] NVARCHAR(20) NOT NULL,
    [EffectiveAt] DATETIMEOFFSET NOT NULL,
    [CreatedAt] DATETIMEOFFSET NOT NULL,
    [ActorId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_OrganizationUsagePolicy] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_OrganizationUsagePolicy_Organization] FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organization] ([Id])
);

CREATE UNIQUE INDEX [IX_OrganizationUsagePolicy_OrganizationId_Version]
    ON [dbo].[OrganizationUsagePolicy] ([OrganizationId], [Version]);

CREATE UNIQUE INDEX [IX_OrganizationUsagePolicy_OrganizationId_EffectiveAt]
    ON [dbo].[OrganizationUsagePolicy] ([OrganizationId], [EffectiveAt]);
