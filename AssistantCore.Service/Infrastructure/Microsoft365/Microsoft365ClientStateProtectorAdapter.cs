using System.Security.Cryptography;
using System.Text;
using AssistantCore.Service.Application.Configuration;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;
using Microsoft.Extensions.Options;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ClientStateProtectorAdapter(
    IOptions<Microsoft365Options> options) : IMicrosoft365ClientStateProtector
{
    public Microsoft365ClientState Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var value = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new Microsoft365ClientState(value, Convert.ToHexString(ComputeHmac(value)));
    }

    public bool Matches(string clientState, string protectedClientState)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(protectedClientState);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = ComputeHmac(clientState);
        return expected.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private byte[] ComputeHmac(string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Value.ClientStateHmacKey));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }
}
