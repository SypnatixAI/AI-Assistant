using System.Security.Cryptography;
using System.Text;
using AssistantCore.Service.Application.Models.Microsoft365;
using AssistantCore.Service.Application.Services.Microsoft365;

namespace AssistantCore.Service.Infrastructure.Microsoft365;

public sealed class Microsoft365ClientStateProtectorAdapter : IMicrosoft365ClientStateProtector
{
    public Microsoft365ClientState Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var value = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new Microsoft365ClientState(value, ComputeHash(value));
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

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(clientState));
        return expected.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static string ComputeHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
