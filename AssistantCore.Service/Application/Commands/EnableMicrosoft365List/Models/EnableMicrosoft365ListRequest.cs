using System.Text.Json.Serialization;

namespace AssistantCore.Service.Application.Commands.EnableMicrosoft365List.Models;

public sealed record EnableMicrosoft365ListRequest(
    [property: JsonRequired] bool IsIndexed);
