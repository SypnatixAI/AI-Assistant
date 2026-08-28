namespace AssistantCore.Service.Application.Configuration;

public sealed class ConversationListingOptions
{
    public const string SectionName = "ConversationListing";

    public int MaximumPreviewLength { get; init; }
}
