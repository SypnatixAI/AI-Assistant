using System.Text.Json;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Messages.Tools;

namespace AssistantCore.Service.Tests.Messages;

public sealed class AiToolArgumentSecurityValidatorAclTests
{
    [Theory]
    [InlineAutoDomainData("allowedUserIds")]
    [InlineAutoDomainData("allowedGroupIds")]
    [InlineAutoDomainData("allowedSharePointGroupIds")]
    [InlineAutoDomainData("hasAnonymousLink")]
    [InlineAutoDomainData("hasOrganizationLink")]
    [InlineAutoDomainData("aclFingerprint")]
    public void Given_AnAclFieldFromTheModel_When_Validate_Then_RejectsTheToolCall(
        string fieldName,
        string toolCallId,
        string fieldValue)
    {
        // Given
        var arguments = JsonSerializer.SerializeToElement(
            new Dictionary<string, object?> { [fieldName] = fieldValue });
        var validator = new AiToolArgumentSecurityValidator();

        // When
        var action = () => validator.Validate(arguments, toolCallId);

        // Then
        var exception = Assert.Throws<ToolCallValidationException>(action);
        Assert.Equal(toolCallId, exception.ToolCallId);
    }
}
