using System.Security.Cryptography;
using System.Text;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.ContentExtraction;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365DocumentChunkingService(IOptions<Microsoft365Options> options)
    : IMicrosoft365DocumentChunkingService
{
    public IReadOnlyList<Microsoft365SearchPassage> CreateChunks(
        Guid organizationId,
        Guid sourceId,
        string siteId,
        string driveId,
        string driveItemId,
        string documentVersion,
        string title,
        string? url,
        DateTimeOffset? modifiedAt,
        IReadOnlyCollection<Microsoft365ExtractedContentUnit> units)
    {
        var maximumCharacters = checked(options.Value.ChunkMaximumTokens * 4);
        var overlapCharacters = checked(options.Value.ChunkOverlapTokens * 4);
        var orderedUnits = units.OrderBy(unit => unit.Order).ToArray();
        var text = string.Join(Environment.NewLine, orderedUnits.Select(unit => unit.Text));
        var sectionPositions = FindSectionPositions(orderedUnits);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var chunks = new List<Microsoft365SearchPassage>();
        var position = 0;
        while (position < text.Length && chunks.Count < options.Value.MaximumChunksPerDocument)
        {
            var length = Math.Min(maximumCharacters, text.Length - position);
            if (position + length < text.Length)
            {
                var boundary = text.LastIndexOfAny(
                    ['\n', '.', '!', '?', ';'],
                    position + length - 1,
                    length);
                if (boundary > position + maximumCharacters / 2)
                {
                    length = boundary - position + 1;
                }
            }

            var content = text.Substring(position, length).Trim();
            var sectionTitle = sectionPositions
                .LastOrDefault(section => section.Position <= position).Title;
            if (content.Length > 0)
            {
                var chunkNumber = chunks.Count;
                chunks.Add(new Microsoft365SearchPassage(
                    CreateChunkId(organizationId, sourceId, driveItemId, documentVersion, chunkNumber),
                    title,
                    content,
                    siteId,
                    driveId,
                    driveItemId,
                    documentVersion,
                    chunkNumber,
                    url,
                    modifiedAt,
                    SectionTitle: string.IsNullOrWhiteSpace(sectionTitle) ? null : sectionTitle));
            }

            if (position + length >= text.Length)
            {
                break;
            }

            position += Math.Max(1, length - overlapCharacters);
        }

        return chunks;
    }

    private static IReadOnlyList<(int Position, string Title)> FindSectionPositions(
        IReadOnlyCollection<Microsoft365ExtractedContentUnit> units)
    {
        var sections = new List<(int Position, string Title)>();
        var position = 0;
        foreach (var unit in units)
        {
            if (unit.Kind is Microsoft365ExtractedContentUnitKind.Header
                or Microsoft365ExtractedContentUnitKind.Title)
            {
                sections.Add((position, unit.Text.Trim()));
            }

            position += unit.Text.Length + Environment.NewLine.Length;
        }

        return sections;
    }

    private static string CreateChunkId(
        Guid organizationId,
        Guid sourceId,
        string driveItemId,
        string version,
        int chunkNumber)
    {
        var identity = $"{organizationId:N}|{sourceId:N}|{driveItemId}|{version}|{chunkNumber}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }
}
