namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365DocumentSupportPolicy
{
    bool IsSupported(string fileName, string? mimeType);
}
