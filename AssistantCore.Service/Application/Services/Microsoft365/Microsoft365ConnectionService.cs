using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AssistantCore.Repository.Abstractions;
using AssistantCore.Repository.Domain.Enums;
using AssistantCore.Repository.Repositories;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Exceptions;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.AuthenticateUser;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Application.Services.Microsoft365;

public sealed class Microsoft365ConnectionService(
    IAuthenticateUserService authenticateUserService,
    IMicrosoft365ConnectionRepository connectionRepository,
    IMicrosoft365ConsentClient consentClient,
    IMicrosoft365ConsentStateProtector stateProtector,
    IMicrosoft365TechnicalTokenStore tokenStore,
    IOptions<Microsoft365Options> options,
    TimeProvider timeProvider) : IMicrosoft365ConnectionService
{
    public async Task<Uri> StartConsentAsync(CancellationToken cancellationToken = default)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(options.Value.ConsentStateLifetimeMinutes);
        var state = stateProtector.Protect(new Microsoft365ConsentState(
            organization.Id,
            Guid.NewGuid(),
            expiresAt));

        await connectionRepository.PrepareConsentAsync(
            organization.Id,
            ComputeStateHash(state),
            expiresAt,
            now,
            cancellationToken);

        return consentClient.CreateAdminConsentUri(state);
    }

    public async Task<Microsoft365ConnectionResult> CompleteConsentAsync(
        string tenantId,
        bool adminConsent,
        string state,
        string? microsoftError,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new BadRequestException("Microsoft 365 consent state is required.");
        }

        Microsoft365ConsentState consentState;
        try
        {
            consentState = stateProtector.Unprotect(state);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            throw new BadRequestException("Microsoft 365 consent state is invalid.");
        }

        var now = timeProvider.GetUtcNow();
        if (consentState.ExpiresAt <= now)
        {
            throw new BadRequestException("Microsoft 365 consent state has expired.");
        }

        var connection = await connectionRepository.FindConsentAsync(
            consentState.OrganizationId,
            ComputeStateHash(state),
            cancellationToken)
            ?? throw new BadRequestException("Microsoft 365 consent state is invalid or has already been replaced.");

        if (connection.ConsentStateConsumedAt is not null)
        {
            throw new BadRequestException("Microsoft 365 consent state has already been used.");
        }

        if (connection.ConsentStateExpiresAt is null || connection.ConsentStateExpiresAt <= now)
        {
            throw new BadRequestException("Microsoft 365 consent state has expired.");
        }

        if (!string.IsNullOrWhiteSpace(microsoftError) || !adminConsent)
        {
            await connectionRepository.MarkConsentErrorAsync(
                connection,
                "MicrosoftConsentDenied",
                now,
                cancellationToken);
            throw new BadRequestException("Microsoft 365 consent was not granted.");
        }

        if (!Guid.TryParse(tenantId, out var parsedTenantId) || parsedTenantId == Guid.Empty)
        {
            throw new BadRequestException("Microsoft 365 tenant is required.");
        }

        var validatedTenantId = parsedTenantId.ToString("D");
        Microsoft365ConsentExchange exchange;
        try
        {
            exchange = await consentClient.CompleteAdminConsentAsync(
                validatedTenantId,
                cancellationToken);
        }
        catch (Microsoft365ExternalException)
        {
            await connectionRepository.MarkConsentErrorAsync(
                connection,
                "MicrosoftAdminConsentValidationFailed",
                now,
                cancellationToken);
            throw;
        }
        if (await connectionRepository.IsTenantConnectedToAnotherOrganizationAsync(
                consentState.OrganizationId,
                validatedTenantId,
                cancellationToken))
        {
            throw new BadRequestException("Microsoft 365 tenant is already connected to another organization.");
        }

        await connectionRepository.CompleteConsentAsync(
            connection,
            validatedTenantId,
            now,
            cancellationToken);
        try
        {
            await tokenStore.StoreAsync(
                connection.Id,
                exchange.AccessToken,
                exchange.AccessTokenExpiresAt,
                cancellationToken);
        }
        catch
        {
            await connectionRepository.MarkConsentErrorAsync(
                connection,
                "MicrosoftTechnicalTokenStorageFailed",
                now,
                cancellationToken);
            throw;
        }

        return new Microsoft365ConnectionResult(
            connection.Id,
            validatedTenantId,
            connection.Status);
    }

    public async Task<Microsoft365ConnectionResult> RevokeAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var (organization, member) = await authenticateUserService.GetOrganizationAsync(cancellationToken);
        if (member.Role != OrganizationRole.Admin)
        {
            throw new ForbiddenException("Administrator access required.");
        }

        var connection = await connectionRepository.FindByIdAsync(
            connectionId,
            organization.Id,
            cancellationToken)
            ?? throw new NotFoundException("Microsoft 365 connection was not found.");

        await connectionRepository.RevokeAsync(
            connection,
            timeProvider.GetUtcNow(),
            cancellationToken);
        await tokenStore.RemoveAsync(connection.Id, cancellationToken);

        return new Microsoft365ConnectionResult(
            connection.Id,
            connection.TenantId ?? string.Empty,
            connection.Status);
    }

    private static string ComputeStateHash(string state) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
}
