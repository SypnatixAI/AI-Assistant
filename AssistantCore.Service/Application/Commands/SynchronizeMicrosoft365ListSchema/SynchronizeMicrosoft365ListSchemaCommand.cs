using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Commands.SynchronizeMicrosoft365ListSchema;

public sealed record SynchronizeMicrosoft365ListSchemaCommand(Guid SourceId)
    : IRequest<Microsoft365ListSchemaSynchronizationResult>;
