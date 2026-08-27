using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Services.Conversations.Pagination;

namespace AssistantCore.Service.Tests.Conversations;

public sealed class ConversationListingLimitsTests
{
    [Fact]
    public void Given_NoLimit_When_Validate_Then_ReturnsTheDefault()
    {
        // When
        var result = ConversationListingLimits.Validate(null);

        // Then
        Assert.Equal(25, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Given_AValidLimit_When_Validate_Then_ReturnsItUnchanged(int limit)
    {
        // When
        var result = ConversationListingLimits.Validate(limit);

        // Then
        Assert.Equal(limit, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void Given_AnOutOfRangeLimit_When_Validate_Then_ThrowsBadRequestException(int limit)
    {
        // When / Then
        Assert.Throws<BadRequestException>(() => ConversationListingLimits.Validate(limit));
    }
}
