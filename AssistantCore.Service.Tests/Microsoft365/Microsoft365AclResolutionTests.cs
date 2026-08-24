using AssistantCore.Service.Application.Models.Microsoft365.Permissions;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365AclResolutionTests
{
    [Theory, AutoDomainData]
    public void Given_AnAcl_When_ResolvedAcl_Then_ReturnsResolvedAcl(Microsoft365Acl acl)
    {
        // Given

        // When
        var result = new Microsoft365AclResolution.ResolvedAcl(acl);

        // Then
        Assert.Same(acl, result.Acl);
    }

    [Theory, AutoDomainData]
    public void Given_AFailureReason_When_Unresolved_Then_ReturnsUnresolvedReason(
        Microsoft365AclResolutionFailureReason failureReason)
    {
        // Given

        // When
        var result = new Microsoft365AclResolution.Unresolved(failureReason);

        // Then
        Assert.Equal(failureReason, result.Reason);
    }
}
