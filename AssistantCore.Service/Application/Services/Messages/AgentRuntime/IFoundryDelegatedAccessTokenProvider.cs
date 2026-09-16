namespace AssistantCore.Service.Application.Services.Messages.AgentRuntime;

public interface IFoundryDelegatedAccessTokenProvider
{
    Task<FoundryDelegatedAccessToken> GetAsync(CancellationToken cancellationToken);
}

public sealed record FoundryDelegatedAccessToken(
    string AccessToken,
    DateTimeOffset ExpiresOn);
