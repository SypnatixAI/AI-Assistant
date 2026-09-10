using AssistantCore.Service.Application.Exceptions;

namespace AssistantCore.Service.Application.Services.Conversations.Pagination;

public static class ConversationMessageListingLimits
{
    public const int DefaultLimit = 50;
    public const int MaximumLimit = 100;

    public static int Validate(int? requestedLimit)
    {
        if (requestedLimit is null)
        {
            return DefaultLimit;
        }

        if (requestedLimit < 1 || requestedLimit > MaximumLimit)
        {
            throw new BadRequestException(
                $"limit must be between 1 and {MaximumLimit}.");
        }

        return requestedLimit.Value;
    }
}
