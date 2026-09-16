using Azure.Core;

namespace AssistantCore.ExternalServices.Services.Foundry;

internal sealed class FixedAccessTokenCredential(
    string accessToken,
    DateTimeOffset expiresOn) : TokenCredential
{
    private readonly AccessToken _token = new(accessToken, expiresOn);

    public override AccessToken GetToken(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken) =>
        _token;

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_token);
}
