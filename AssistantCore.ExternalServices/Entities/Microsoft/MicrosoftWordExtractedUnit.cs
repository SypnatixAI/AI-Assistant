namespace AssistantCore.ExternalServices.Entities.Microsoft;

public sealed record MicrosoftWordExtractedUnit(
    MicrosoftWordExtractedUnitKind Kind,
    int Order,
    string Text,
    string SourcePart);

public enum MicrosoftWordExtractedUnitKind
{
    Header,
    Title,
    Paragraph,
    ListItem,
    Table,
    Footer
}
