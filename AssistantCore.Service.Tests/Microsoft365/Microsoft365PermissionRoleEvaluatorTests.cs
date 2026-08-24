using AssistantCore.Service.Application.Models.Microsoft365.Permissions;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class Microsoft365PermissionRoleEvaluatorTests
{
    [Theory]
    [InlineAutoDomainData("read")]
    [InlineAutoDomainData("write")]
    [InlineAutoDomainData("owner")]
    public void Given_ADriveItemReadRole_When_EvaluateDriveItemRoles_Then_ReturnsReadAllowed(
        string role)
    {
        // Given
        var evaluator = new Microsoft365PermissionRoleEvaluator();

        // When
        var result = evaluator.EvaluateDriveItemRoles([role]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.ReadAllowed, result);
    }

    [Theory, InlineAutoDomainData("unknown")]
    public void Given_AnUnknownDriveItemRole_When_EvaluateDriveItemRoles_Then_ReturnsUnresolved(
        string role)
    {
        // Given
        var evaluator = new Microsoft365PermissionRoleEvaluator();

        // When
        var result = evaluator.EvaluateDriveItemRoles(["read", role]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.Unresolved, result);
    }

    [Theory, AutoDomainData]
    public void Given_NoDriveItemRole_When_EvaluateDriveItemRoles_Then_ReturnsUnresolved(
        Microsoft365PermissionRoleEvaluator evaluator)
    {
        // Given

        // When
        var result = evaluator.EvaluateDriveItemRoles([]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.Unresolved, result);
    }

    [Theory, AutoDomainData]
    public void Given_LimitedAccessAlone_When_EvaluateSharePointRoleTypes_Then_ReturnsNoReadAccess(
        Microsoft365PermissionRoleEvaluator evaluator)
    {
        // Given

        // When
        var result = evaluator.EvaluateSharePointRoleTypes([1]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.NoReadAccess, result);
    }

    [Theory, AutoDomainData]
    public void Given_LimitedAccessAndReader_When_EvaluateSharePointRoleTypes_Then_ReturnsReadAllowed(
        Microsoft365PermissionRoleEvaluator evaluator)
    {
        // Given

        // When
        var result = evaluator.EvaluateSharePointRoleTypes([1, 2]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.ReadAllowed, result);
    }

    [Theory]
    [InlineAutoDomainData(2)]
    [InlineAutoDomainData(3)]
    [InlineAutoDomainData(4)]
    [InlineAutoDomainData(5)]
    [InlineAutoDomainData(6)]
    [InlineAutoDomainData(7)]
    [InlineAutoDomainData(8)]
    public void Given_ASharePointReadRole_When_EvaluateSharePointRoleTypes_Then_ReturnsReadAllowed(
        int roleTypeKind)
    {
        // Given
        var evaluator = new Microsoft365PermissionRoleEvaluator();

        // When
        var result = evaluator.EvaluateSharePointRoleTypes([roleTypeKind]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.ReadAllowed, result);
    }

    [Theory, AutoDomainData]
    public void Given_NoSharePointRole_When_EvaluateSharePointRoleTypes_Then_ReturnsUnresolved(
        Microsoft365PermissionRoleEvaluator evaluator)
    {
        // Given

        // When
        var result = evaluator.EvaluateSharePointRoleTypes([]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.Unresolved, result);
    }

    [Theory]
    [InlineAutoDomainData(0)]
    [InlineAutoDomainData(255)]
    public void Given_AnUnsupportedSharePointRole_When_EvaluateSharePointRoleTypes_Then_ReturnsUnresolved(
        int roleTypeKind)
    {
        // Given
        var evaluator = new Microsoft365PermissionRoleEvaluator();

        // When
        var result = evaluator.EvaluateSharePointRoleTypes([roleTypeKind]);

        // Then
        Assert.Equal(Microsoft365PermissionRoleEvaluation.Unresolved, result);
    }
}
