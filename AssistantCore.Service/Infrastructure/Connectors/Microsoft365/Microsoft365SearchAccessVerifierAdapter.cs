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
        var verificationTasks = new Dictionary<DocumentReference, Task<bool>>();
        var checks = records
            .Select(record => new RecordAuthorizationCheck(
                record,
                GetOrCreateVerificationTask(
                    record,
                    organization,
                    entraUserId,
                    normalizedGroupIds,
                    normalizedSharePointGroupIds,
                    verificationTasks,
                    cancellationToken)))
            .ToArray();

        await Task.WhenAll(verificationTasks.Values);

        var authorizedRecords = checks
            .Where(check => check.Verification.GetAwaiter().GetResult())
            .Select(check => check.Record)
            .ToArray();

        logger?.LogInformation(
            "Microsoft365 ACL verification completed in {ElapsedMilliseconds} ms: {CheckedCount} records mapped to {UniqueDocumentCount} unique document checks, {AuthorizedCount} authorized.",
            TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds,
            records.Count,
            verificationTasks.Count,
            authorizedRecords.Length);

        return authorizedRecords;
    }

    private Task<bool> GetOrCreateVerificationTask(
        Microsoft365SearchRecord record,
        Organization organization,
        string entraUserId,
        IReadOnlySet<string> entraGroupIds,
        IReadOnlySet<string> sharePointGroupIds,
        IDictionary<DocumentReference, Task<bool>> verificationTasks,
        CancellationToken cancellationToken)
    {
        if (!TryCreateDocumentReference(record, out var reference))
        {
            return Task.FromResult(false);
        }

        if (verificationTasks.TryGetValue(reference, out var existingTask))
        {
            return existingTask;
        }

        var task = IsAuthorizedAsync(
            organization,
            entraUserId,
            entraGroupIds,
            sharePointGroupIds,
            record,
            cancellationToken);
        verificationTasks.Add(reference, task);
        return task;
    }

    private static bool TryCreateDocumentReference(
        Microsoft365SearchRecord record,
        out DocumentReference reference)
    {
        if (string.IsNullOrWhiteSpace(record.SiteId)
            || string.IsNullOrWhiteSpace(record.DriveId)
            || string.IsNullOrWhiteSpace(record.DriveItemId))
        {
            reference = default;
            return false;
        }

        reference = new DocumentReference(
            record.SiteId,
            record.DriveId,
            record.DriveItemId);
        return true;
    }

    private async Task<bool> IsAuthorizedAsync(
        Organization organization,
        string entraUserId,
        IReadOnlySet<string> entraGroupIds,
        IReadOnlySet<string> sharePointGroupIds,
        Microsoft365SearchRecord record,
        CancellationToken cancellationToken)
    {
        var resolution = await aclResolver.ResolveAsync(
            organization,
            new Microsoft365ContentReference(
                Microsoft365ContentReferenceKind.DriveItem,
                record.SiteId!,
                record.DriveId!,
                ListId: null,
                record.DriveItemId!),
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

    private readonly record struct DocumentReference(
        string SiteId,
        string DriveId,
        string DriveItemId);

    private sealed record RecordAuthorizationCheck(
        Microsoft365SearchRecord Record,
        Task<bool> Verification);
}
