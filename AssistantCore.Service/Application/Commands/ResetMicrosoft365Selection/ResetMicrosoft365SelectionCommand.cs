using AssistantCore.Service.Application.Abstractions;
using AssistantCore.Service.Application.Commands.ResetMicrosoft365Selection.Models;

namespace AssistantCore.Service.Application.Commands.ResetMicrosoft365Selection;

/// <summary>
/// L'organisation n'est pas un parametre : elle vient du contexte authentifie,
/// afin qu'un administrateur ne puisse jamais reinitialiser une autre organisation.
/// </summary>
public sealed record ResetMicrosoft365SelectionCommand
    : IRequest<ResetMicrosoft365SelectionResponse>;
