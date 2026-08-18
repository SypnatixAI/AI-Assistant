using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Models.Microsoft365;

public sealed record Microsoft365ConnectionResult(
    Guid ConnectionId,
    string TenantId,
    Microsoft365ConnectionStatus Status);
