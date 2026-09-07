USE [AssistantCoreDb];

CREATE TABLE [dbo].[TokenConsumption] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [AssistantMessageId] UNIQUEIDENTIFIER NOT NULL,
    [PeriodStartsAt] DATETIMEOFFSET NOT NULL,
    [PeriodEndsAt] DATETIMEOFFSET NOT NULL,
    [InputTokens] BIGINT NOT NULL,
    [OutputTokens] BIGINT NOT NULL,
    [TotalTokens] BIGINT NOT NULL,
    [CreatedAt] DATETIMEOFFSET NOT NULL,
    CONSTRAINT [PK_TokenConsumption] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_TokenConsumption_Organization] FOREIGN KEY ([OrganizationId])
        REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_TokenConsumption_Message] FOREIGN KEY ([AssistantMessageId])
        REFERENCES [dbo].[Message] ([Id])
);

CREATE UNIQUE INDEX [IX_TokenConsumption_OneRecordPerMessage]
    ON [dbo].[TokenConsumption] ([AssistantMessageId]);

CREATE INDEX [IX_TokenConsumption_OrganizationId_PeriodStartsAt]
    ON [dbo].[TokenConsumption] ([OrganizationId], [PeriodStartsAt]);
