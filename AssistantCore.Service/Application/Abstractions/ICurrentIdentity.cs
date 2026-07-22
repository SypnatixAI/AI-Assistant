namespace AssistantCore.Service.Application.Abstractions;

public interface ICurrentIdentity
{
    Guid TenantId { get; }

    Guid ObjectId { get; }

    string? DisplayName { get; }

    string? Email { get; }
}