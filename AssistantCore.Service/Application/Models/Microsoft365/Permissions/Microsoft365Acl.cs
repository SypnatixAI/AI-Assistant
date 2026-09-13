using System.Security.Cryptography;
using System.Text.Json;

namespace AssistantCore.Service.Application.Models.Microsoft365.Permissions;

public sealed record Microsoft365Acl
{
    public Microsoft365Acl(
        IReadOnlyCollection<string> allowedEntraUserIds,
        IReadOnlyCollection<string> allowedEntraGroupIds,
        IReadOnlyCollection<string> allowedSharePointGroupIds,
        bool hasAnonymousLink,
        bool hasOrganizationLink,
        Microsoft365AclInheritance inheritance)
    {
        AllowedEntraUserIds = NormalizeIdentifiers(
            allowedEntraUserIds,
            nameof(allowedEntraUserIds));
        AllowedEntraGroupIds = NormalizeIdentifiers(
            allowedEntraGroupIds,
            nameof(allowedEntraGroupIds));
        AllowedSharePointGroupIds = NormalizeIdentifiers(
            allowedSharePointGroupIds,
            nameof(allowedSharePointGroupIds));
        HasAnonymousLink = hasAnonymousLink;
        HasOrganizationLink = hasOrganizationLink;
        Inheritance = inheritance;
        Fingerprint = CreateFingerprint();
    }

    public IReadOnlyCollection<string> AllowedEntraUserIds { get; }

    public IReadOnlyCollection<string> AllowedEntraGroupIds { get; }

    public IReadOnlyCollection<string> AllowedSharePointGroupIds { get; }

    public bool HasAnonymousLink { get; }

    public bool HasOrganizationLink { get; }

    public Microsoft365AclInheritance Inheritance { get; }

    public string Fingerprint { get; }

    private static IReadOnlyCollection<string> NormalizeIdentifiers(
        IReadOnlyCollection<string> identifiers,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(identifiers, parameterName);

        if (identifiers.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "ACL identifiers cannot be empty.",
                parameterName);
        }

        return Array.AsReadOnly(
            identifiers
                .Distinct(StringComparer.Ordinal)
                .OrderBy(identifier => identifier, StringComparer.Ordinal)
                .ToArray());
    }

    private string CreateFingerprint()
    {
        using var canonicalAcl = new MemoryStream();
        using (var writer = new Utf8JsonWriter(canonicalAcl))
        {
            writer.WriteStartObject();
            WriteIdentifiers(writer, "allowedEntraUserIds", AllowedEntraUserIds);
            WriteIdentifiers(writer, "allowedEntraGroupIds", AllowedEntraGroupIds);
            WriteIdentifiers(writer, "allowedSharePointGroupIds", AllowedSharePointGroupIds);
            writer.WriteBoolean("hasAnonymousLink", HasAnonymousLink);
            writer.WriteBoolean("hasOrganizationLink", HasOrganizationLink);
            writer.WriteNumber("inheritance", (int)Inheritance);
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(canonicalAcl.ToArray()));
    }

    private static void WriteIdentifiers(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> identifiers)
    {
        writer.WriteStartArray(propertyName);
        foreach (var identifier in identifiers)
        {
            writer.WriteStringValue(identifier);
        }

        writer.WriteEndArray();
    }
}
