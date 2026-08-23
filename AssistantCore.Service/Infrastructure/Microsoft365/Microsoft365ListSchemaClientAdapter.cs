using AssistantCore.ExternalServices.Services.Microsoft;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ListSchemaClientAdapter(
    MicrosoftIdentityClient identityClient,
    MicrosoftGraphListSchemaClient graphClient,
    IOptions<Microsoft365Options> options) : IMicrosoft365ListSchemaClient
{
    public async Task<IReadOnlyCollection<Microsoft365ListColumn>> GetColumnsAsync(
        string tenantId,
        string siteId,
        string listId,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        try
        {
            var token = await identityClient.AcquireApplicationTokenAsync(
                configuration.AuthorityBaseUrl,
                tenantId,
                configuration.ClientId,
                configuration.ClientSecret,
                cancellationToken);
            var columns = await graphClient.GetColumnsAsync(
                configuration.GraphBaseUrl,
                token.AccessToken,
                siteId,
                listId,
                cancellationToken);

            return columns
                .Select(column => new Microsoft365ListColumn(column.Id, column.Definition.Clone()))
                .ToArray();
        }
        catch (MicrosoftExternalException exception)
        {
            throw new Microsoft365ExternalException(
                "Microsoft 365 list schema could not be loaded.",
                exception);
        }
    }
}
