namespace AssistantCore.Ingestion.Worker;

public sealed class Microsoft365WorkerOptions
{
    public const string SectionName = "Microsoft365Worker";

    public bool RunStartupConnectionCheck { get; init; }

    public Guid? StartupConnectionId { get; init; }
}
