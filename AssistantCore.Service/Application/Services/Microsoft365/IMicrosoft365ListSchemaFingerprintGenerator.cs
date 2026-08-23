using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ListSchemaFingerprintGenerator
{
    string CreateFingerprint(IReadOnlyCollection<Microsoft365ListColumn> columns);
}
