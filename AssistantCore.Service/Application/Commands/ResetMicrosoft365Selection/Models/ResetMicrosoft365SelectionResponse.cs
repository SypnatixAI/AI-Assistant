namespace AssistantCore.Service.Application.Commands.ResetMicrosoft365Selection.Models;

public sealed record ResetMicrosoft365SelectionResponse(
    Guid OrganizationId,
    int DeletedSubscriptions,
    int DeletedSynchronizations,
    int DeletedWorkItems,
    int DeletedIndexedContents,
    int DeletedSources,
    int DeletedSearchChunks);
