namespace AssistantCore.Service.Application.Configuration;

public sealed class AgentRuntimeOptions
{
    public const string SectionName = "Messages:AgentRuntime";

    public int MaximumExecutionTimeSeconds { get; init; }

    public int RetrievalCandidateLimit { get; init; }

    public int FinalEvidenceLimit { get; init; }
}
