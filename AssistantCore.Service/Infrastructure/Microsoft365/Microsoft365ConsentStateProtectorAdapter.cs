using System.Text.Json;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.AspNetCore.DataProtection;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ConsentStateProtectorAdapter(
    IDataProtectionProvider dataProtectionProvider) : IMicrosoft365ConsentStateProtector
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector(
        "AssistantCore.Microsoft365.ConsentState.v1");

    public string Protect(Microsoft365ConsentState state) =>
        protector.Protect(JsonSerializer.Serialize(state));

    public Microsoft365ConsentState Unprotect(string protectedState) =>
        JsonSerializer.Deserialize<Microsoft365ConsentState>(protector.Unprotect(protectedState))
        ?? throw new FormatException("Microsoft 365 consent state payload is empty.");
}
