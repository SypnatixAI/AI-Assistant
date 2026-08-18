namespace AssistantCore.Service.Application.Configuration;

public sealed class MessagesOptions
{
    public const string SectionName = "Messages";

    public int MaximumMessageLength { get; init; }
}
