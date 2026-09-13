using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Backoffice;

namespace AssistantCore.Service.Application.Commands.GetBackofficeOrganizationDetails;

public sealed record GetBackofficeOrganizationDetailsQuery(
    Guid OrganizationId) : IRequest<BackofficeOrganizationDetailsDto>;
