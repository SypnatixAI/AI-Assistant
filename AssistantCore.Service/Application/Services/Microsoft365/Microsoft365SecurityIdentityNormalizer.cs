using System.Globalization;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365SecurityIdentityNormalizer
    : IMicrosoft365SecurityIdentityNormalizer
{
    public string NormalizeEntraUserId(string objectId) =>
        NormalizeEntraObjectId(objectId, nameof(objectId));

    public string NormalizeEntraGroupId(string objectId) =>
        NormalizeEntraObjectId(objectId, nameof(objectId));

    public string NormalizeSharePointGroupId(
        string siteId,
        string sharePointGroupId)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            throw new ArgumentException(
                "The Microsoft SharePoint site identifier is required.",
                nameof(siteId));
        }

        string normalizedGroupId;
        if (int.TryParse(
                sharePointGroupId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedGroupId)
            && parsedGroupId > 0)
        {
            normalizedGroupId = parsedGroupId.ToString(CultureInfo.InvariantCulture);
        }
        else if (Guid.TryParse(sharePointGroupId, out var parsedGroupObjectId)
                 && parsedGroupObjectId != Guid.Empty)
        {
            normalizedGroupId = parsedGroupObjectId.ToString("D");
        }
        else
        {
            throw new ArgumentException(
                "A valid Microsoft SharePoint group identifier is required.",
                nameof(sharePointGroupId));
        }

        return $"spg:{siteId.Trim()}:{normalizedGroupId}";
    }

    private static string NormalizeEntraObjectId(
        string objectId,
        string parameterName)
    {
        if (!Guid.TryParse(objectId, out var parsedObjectId)
            || parsedObjectId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid Microsoft Entra Object ID is required.",
                parameterName);
        }

        return parsedObjectId.ToString("D");
    }
}
