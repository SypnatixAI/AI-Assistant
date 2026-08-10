using AssistantCore.Service.Application.Services.Messages;

namespace AssistantCore.Service.Tests.Messages;

public sealed class SendMessageServiceTests
{
    [Fact]
    public async Task Given_AnyRequest_When_SendMessageAsync_Then_ThrowsNotImplementedException()
    {
        // Given
        var service = new SendMessageService();

        // When
        var exception = await Record.ExceptionAsync(() => service.SendMessageAsync(
            null,
            "Question",
            "gpt",
            CancellationToken.None));

        // Then
        var notImplementedException = Assert.IsType<NotImplementedException>(exception);
        Assert.Equal(
            "The message flow has not been implemented yet.",
            notImplementedException.Message);
    }
}
