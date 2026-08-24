using AssistantCore.ExternalServices.Entities.Azure;

namespace AssistantCore.ExternalServices.Services.Azure;

public static class AzureAiSearchMicrosoft365IndexDefinition
{
    public static IReadOnlyCollection<AzureAiSearchIndexFieldDefinition> CreateFields() =>
    [
        new("chunkId", "Edm.String", Key: true, Filterable: true),
        new("organizationId", "Edm.String", Filterable: true, Retrievable: false),
        new("title", "Edm.String", Searchable: true),
        new("content", "Edm.String", Searchable: true),
        new("allowedUserIds", "Collection(Edm.String)", Filterable: true, Retrievable: false),
        new("allowedGroupIds", "Collection(Edm.String)", Filterable: true, Retrievable: false),
        new("allowedSharePointGroupIds", "Collection(Edm.String)", Filterable: true, Retrievable: false),
        new("hasAnonymousLink", "Edm.Boolean", Filterable: true, Retrievable: false),
        new("aclFingerprint", "Edm.String", Filterable: true, Retrievable: false),
        new("isAvailable", "Edm.Boolean", Filterable: true)
    ];
}
