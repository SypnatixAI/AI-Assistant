using AssistantCore.Repository.Domain.Entities;
using AssistantCore.Service.Application.Services.Members;

namespace AssistantCore.Service.Tests;

internal sealed class StubMemberManagementService : IMemberManagementService
{
    public IReadOnlyCollection<OrganizationMember> Members { get; init; } = [];

    public OrganizationMember? UpdatedMember { get; init; }

    public Guid? ReceivedMemberId { get; private set; }

    public string? ReceivedRole { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<IReadOnlyCollection<OrganizationMember>> GetMembersAsync(
        CancellationToken cancellationToken = default)
    {
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(Members);
    }

    public Task<OrganizationMember> UpdateMemberRoleAsync(
        Guid memberId,
        string role,
        CancellationToken cancellationToken = default)
    {
        ReceivedMemberId = memberId;
        ReceivedRole = role;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult(
            UpdatedMember ?? throw new InvalidOperationException("Updated member is not configured."));
    }
}
