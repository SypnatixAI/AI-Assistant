using AssistantCore.Service.Application.Models.Microsoft365.Permissions;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365AclTests
{
    [Theory, AutoDomainData]
    public void Given_DuplicatedUnorderedIdentifiers_When_Microsoft365Acl_Then_DeduplicatesAndSortsBeforeFingerprint(
        string firstUserId,
        string secondUserId,
        string groupId,
        string sharePointGroupId,
        Microsoft365AclInheritance inheritance)
    {
        // Given
        var firstAcl = new Microsoft365Acl(
            [secondUserId, firstUserId, secondUserId],
            [groupId, groupId],
            [sharePointGroupId, sharePointGroupId],
            false,
            false,
            inheritance);
        var equivalentAcl = new Microsoft365Acl(
            [firstUserId, secondUserId],
            [groupId],
            [sharePointGroupId],
            false,
            false,
            inheritance);

        // When
        var fingerprint = firstAcl.Fingerprint;

        // Then
        Assert.Equal(
            new[] { firstUserId, secondUserId }.OrderBy(value => value, StringComparer.Ordinal),
            firstAcl.AllowedEntraUserIds);
        Assert.Single(firstAcl.AllowedEntraGroupIds);
        Assert.Single(firstAcl.AllowedSharePointGroupIds);
        Assert.Equal(equivalentAcl.Fingerprint, fingerprint);
        Assert.Equal(64, fingerprint.Length);
    }

    [Theory, AutoDomainData]
    public void Given_AChangedIdentifier_When_Microsoft365Acl_Then_ChangesFingerprint(
        string firstUserId,
        string secondUserId,
        Microsoft365AclInheritance inheritance)
    {
        // Given
        var original = new Microsoft365Acl(
            [firstUserId],
            [],
            [],
            false,
            false,
            inheritance);
        var changed = new Microsoft365Acl(
            [secondUserId],
            [],
            [],
            false,
            false,
            inheritance);

        // When
        var originalFingerprint = original.Fingerprint;
        var changedFingerprint = changed.Fingerprint;

        // Then
        Assert.NotEqual(originalFingerprint, changedFingerprint);
    }

    [Theory, AutoDomainData]
    public void Given_AChangedOrganizationLink_When_Microsoft365Acl_Then_ChangesFingerprint(
        string userId,
        Microsoft365AclInheritance inheritance)
    {
        // Given
        var withoutOrganizationLink = new Microsoft365Acl(
            [userId],
            [],
            [],
            false,
            false,
            inheritance);
        var withOrganizationLink = new Microsoft365Acl(
            [userId],
            [],
            [],
            false,
            true,
            inheritance);

        // When
        var originalFingerprint = withoutOrganizationLink.Fingerprint;
        var changedFingerprint = withOrganizationLink.Fingerprint;

        // Then
        Assert.NotEqual(originalFingerprint, changedFingerprint);
    }
}
