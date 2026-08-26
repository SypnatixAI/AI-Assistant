namespace AssistantCore.Service.Application.Configuration;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public bool Enabled { get; init; }

    public string FullyQualifiedNamespace { get; init; } = string.Empty;

    public string DriveSyncQueue { get; init; } = "sharepoint-drive-sync";

    public string ListSyncQueue { get; init; } = "sharepoint-list-sync";
}
