using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365SecurityIdentityNormalizerTests
{
    [Theory, AutoDomainData]
    public void Given_AnEntraUserObjectId_When_NormalizeEntraUserId_Then_ReturnsCanonicalObjectId(
        Guid objectId)
    {
        // Given
        var normalizer = new Microsoft365SecurityIdentityNormalizer();

        // When
        var result = normalizer.NormalizeEntraUserId(
            objectId.ToString("D").ToUpperInvariant());

        // Then
        Assert.Equal(objectId.ToString("D"), result);
    }

    [Theory, AutoDomainData]
    public void Given_AnEntraGroupObjectId_When_NormalizeEntraGroupId_Then_ReturnsCanonicalObjectId(
        Guid objectId)
    {
        // Given
        var normalizer = new Microsoft365SecurityIdentityNormalizer();

        // When
        var result = normalizer.NormalizeEntraGroupId(
            objectId.ToString("D").ToUpperInvariant());

        // Then
        Assert.Equal(objectId.ToString("D"), result);
    }

    [Theory]
    [InlineAutoDomainData("Ada Lovelace")]
    [InlineAutoDomainData("ada@contoso.com")]
    [InlineAutoDomainData("")]
    [InlineAutoDomainData("00000000-0000-0000-0000-000000000000")]
    public void Given_AProfileValue_When_NormalizeEntraUserId_Then_ThrowsArgumentException(
        string value)
    {
        // Given
        var normalizer = new Microsoft365SecurityIdentityNormalizer();

        // When
        var action = () => normalizer.NormalizeEntraUserId(value);

        // Then
        Assert.Throws<ArgumentException>(action);
    }

    [Theory]
    [InlineAutoDomainData("Finance")]
    [InlineAutoDomainData("finance@contoso.com")]
    [InlineAutoDomainData("")]
    public void Given_AProfileValue_When_NormalizeEntraGroupId_Then_ThrowsArgumentException(
        string value)
    {
        // Given
        var normalizer = new Microsoft365SecurityIdentityNormalizer();

        // When
        var action = () => normalizer.NormalizeEntraGroupId(value);

        // Then
        Assert.Throws<ArgumentException>(action);
    }

    [Theory]
    [InlineAutoDomainData(
        "contoso.sharepoint.com,site-collection-id,web-id",
        "17")]
    public void Given_ASharePointGroup_When_NormalizeSharePointGroupId_Then_ReturnsScopedKey(
        string siteId,
        string sharePointGroupId)
    {
        // Given
        var normalizer = new Microsoft365SecurityIdentityNormalizer();

        // When
        var result = normalizer.NormalizeSharePointGroupId(
            siteId,
            sharePointGroupId);

        // Then
        Assert.Equal($"spg:{siteId}:17", result);
    }

    [Theory, AutoDomainData]
    public void Given_ASharePointGroupObjectId_When_NormalizeSharePointGroupId_Then_ReturnsScopedKey(
        Guid sharePointGroupId,
        string siteId)
    {
        // Given
        var normalizer = new Microsoft365SecurityIdentityNormalizer();

        // When
        var result = normalizer.NormalizeSharePointGroupId(
            siteId,
            sharePointGroupId.ToString("D").ToUpperInvariant());

        // Then
        Assert.Equal($"spg:{siteId}:{sharePointGroupId:D}", result);
    }

    [Theory]
    [InlineAutoDomainData("", "17")]
    [InlineAutoDomainData("site-id", "Finance")]
    [InlineAutoDomainData("site-id", "0")]
    public void Given_AnInvalidSharePointGroup_When_NormalizeSharePointGroupId_Then_ThrowsArgumentException(
        string siteId,
        string sharePointGroupId)
    {
        // Given
        var normalizer = new Microsoft365SecurityIdentityNormalizer();

        // When
        var action = () => normalizer.NormalizeSharePointGroupId(
            siteId,
            sharePointGroupId);

        // Then
        Assert.Throws<ArgumentException>(action);
    }
}
