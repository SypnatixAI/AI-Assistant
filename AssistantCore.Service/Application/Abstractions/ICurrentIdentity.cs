using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Abstractions;

public interface ICurrentIdentity
{
    IdentityProvider IdentityProvider { get; }

    string ExternalTenantId { get; }

    string ExternalUserId { get; }

    string? DisplayName { get; }

    string? Email { get; }
}
