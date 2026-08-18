using AssistantCore.Service.Application.Models.Microsoft365;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public interface IMicrosoft365ConsentClient
{
    Uri CreateAuthorizationUri(string state);

    Task<Microsoft365ConsentExchange> ExchangeCodeAsync(
        string code,
        CancellationToken cancellationToken = default);
}
