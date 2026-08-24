namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365SecurityIdentityNormalizer
{
    string NormalizeEntraUserId(string objectId);

    string NormalizeEntraGroupId(string objectId);

    string NormalizeSharePointGroupId(
        string siteId,
        string sharePointGroupId);
}
