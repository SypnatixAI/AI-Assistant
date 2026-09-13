using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Backoffice;

namespace AssistantCore.Service.Application.Commands.GetBackofficeOrganizations;

public sealed record GetBackofficeOrganizationsQuery(
    int Page,
    int PageSize,
    string? Search) : IRequest<BackofficeOrganizationListResponse>;
