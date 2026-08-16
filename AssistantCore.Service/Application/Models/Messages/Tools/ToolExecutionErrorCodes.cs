namespace AssistantCore.Service.Application.Models.Messages.Tools;

public static class ToolExecutionErrorCodes
{
    public const string ExecutorNotFound = "TOOL_EXECUTOR_NOT_FOUND";

    public const string ExecutorAmbiguous = "TOOL_EXECUTOR_AMBIGUOUS";

    public const string ArgumentMappingFailed = "TOOL_ARGUMENT_MAPPING_FAILED";
}
