using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Models.Backoffice;

internal static class BackofficeOrganizationStatusFormatter
{
    public static string Format(RecordStatus status) =>
        status == RecordStatus.Active ? "Active" : "Disabled";
}
