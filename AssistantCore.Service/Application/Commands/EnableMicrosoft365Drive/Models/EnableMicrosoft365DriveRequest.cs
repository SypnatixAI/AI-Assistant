using System.Text.Json.Serialization;

namespace AssistantCore.Service.Application.Commands.EnableMicrosoft365Drive.Models;

public sealed record EnableMicrosoft365DriveRequest([property: JsonRequired] bool IsIndexed);
