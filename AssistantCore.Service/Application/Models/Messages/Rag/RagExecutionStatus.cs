namespace AssistantCore.Service.Application.Models.Messages.Rag;

// Shared only within a message, including parallel tool calls.
public sealed class RagExecutionStatus
{
    private int correctionAttempts;
    private int insufficientRetrieval;
    public int CorrectionAttempts => Volatile.Read(ref correctionAttempts);
    public bool HasInsufficientRetrieval => Volatile.Read(ref insufficientRetrieval) != 0;
    public void RecordCorrection() => Interlocked.Increment(ref correctionAttempts);
    public void RecordQuality(RetrievalQualityResult quality)
    {
        if (!quality.IsSufficient) Interlocked.Exchange(ref insufficientRetrieval, 1);
    }
}
