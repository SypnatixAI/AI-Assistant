using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Models.Messages.Connectors.Microsoft365;
using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;
using AssistantCore.Service.Application.Services.Messages.Connectors.Microsoft365;
using Microsoft.Extensions.Logging;

namespace AssistantCore.Service.Infrastructure.Connectors.Microsoft365;

public sealed class Microsoft365SearchAccessVerifierAdapter(
    IMicrosoft365AclResolver aclResolver,
    ILogger<Microsoft365SearchAccessVerifierAdapter>? logger = null) : IMicrosoft365SearchAccessVerifier
{
    public async Task<IReadOnlyCollection<Microsoft365SearchRecord>> KeepAuthorizedAsync(
        Guid organizationId,
        string externalTenantId,
        string entraUserId,
        IReadOnlyCollection<string> entraGroupIds,
        IReadOnlyCollection<string> sharePointGroupIds,
        IReadOnlyCollection<Microsoft365SearchRecord> records,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalTenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entraUserId);
        ArgumentNullException.ThrowIfNull(entraGroupIds);
        ArgumentNullException.ThrowIfNull(sharePointGroupIds);
        ArgumentNullException.ThrowIfNull(records);

        var startedAt = TimeProvider.System.GetTimestamp();
        var organization = new Organization
        {
            Id = organizationId,
            ExternalTenantId = externalTenantId
        };
        var normalizedGroupIds = entraGroupIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedSharePointGroupIds = sharePointGroupIds.ToHashSet(StringComparer.Ordinal);
        var decisions = await Task.WhenAll(records.Select(record => IsAuthorizedAsync(
            organization,
            entraUserId,
            normalizedGroupIds,
            normalizedSharePointGroupIds,
            record,
            cancellationToken)));

        var authorizedRecords = records
            .Zip(decisions)
            .Where(item => item.Second)
            .Select(item => item.First)
            .ToArray();

        logger?.LogInformation(
            "Microsoft365 ACL verification completed in {ElapsedMilliseconds} ms: {CheckedCount} documents checked, {AuthorizedCount} authorized.",
            TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds,
            records.Count,
            authorizedRecords.Length);

        return authorizedRecords;
    }

    private async Task<bool> IsAuthorizedAsync(
        Organization organization,
        string entraUserId,
        IReadOnlySet<string> entraGroupIds,
        IReadOnlySet<string> sharePointGroupIds,
        Microsoft365SearchRecord record,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.SiteId)
            || string.IsNullOrWhiteSpace(record.DriveId)
            || string.IsNullOrWhiteSpace(record.DriveItemId))
        {
            return false;
        }

        var resolution = await aclResolver.ResolveAsync(
            organization,
            new Microsoft365ContentReference(
                Microsoft365ContentReferenceKind.DriveItem,
                record.SiteId,
                record.DriveId,
                ListId: null,
                record.DriveItemId),
            cancellationToken);
        if (resolution is not Microsoft365AclResolution.ResolvedAcl resolved)
        {
            return false;
        }

        var acl = resolved.Acl;
        return acl.AllowedEntraUserIds.Contains(entraUserId, StringComparer.OrdinalIgnoreCase)
            || acl.AllowedEntraGroupIds.Any(entraGroupIds.Contains)
            || acl.AllowedSharePointGroupIds.Any(sharePointGroupIds.Contains);
    }
}
