namespace AssistantCore.Service.Application.Commands.SendMessage.Models;

public sealed record SendMessageStreamEvent(string Name, object Data)
{
    public const string Accepted = "message.accepted";
    public const string ProgressUpdated = "progress.updated";
    public const string AnswerDelta = "answer.delta";
    public const string AnswerCompleted = "answer.completed";
    public const string Error = "error";
}
