using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AssistantCore.ExternalServices.Services.Microsoft;

namespace AssistantCore.Service.Tests.Microsoft365;

public sealed class MicrosoftCertificateIdentityClientTests
{
    [Theory, AutoDomainData]
    public async Task Given_AValidPkcs12File_When_AcquireApplicationTokenForScopeAsync_Then_LoadsCertificateBeforeValidatingAuthority(
        string tenantId,
        string clientId,
        string scope)
    {
        // Given
        const string certificatePassword = "test-certificate-password";
        var certificatePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.pfx");
        await File.WriteAllBytesAsync(
            certificatePath,
            CreatePkcs12(certificatePassword));
        var client = new MicrosoftCertificateIdentityClient();

        try
        {
            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.AcquireApplicationTokenForScopeAsync(
                    "http://login.microsoftonline.com",
                    tenantId,
                    clientId,
                    certificatePath,
                    certificatePassword,
                    scope,
                    CancellationToken.None));

            // Then
            Assert.Equal("authorityBaseUrl", exception.ParamName);
        }
        finally
        {
            File.Delete(certificatePath);
        }
    }

    [Theory, AutoDomainData]
    public async Task Given_AValidBase64Pkcs12_When_AcquireApplicationTokenFromBase64ForScopeAsync_Then_LoadsCertificateBeforeValidatingAuthority(
        string tenantId,
        string clientId,
        string scope)
    {
        // Given
        const string certificatePassword = "test-certificate-password";
        var certificateBase64 = Convert.ToBase64String(CreatePkcs12(certificatePassword));
        var client = new MicrosoftCertificateIdentityClient();

        // When
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.AcquireApplicationTokenFromBase64ForScopeAsync(
                "http://login.microsoftonline.com",
                tenantId,
                clientId,
                certificateBase64,
                certificatePassword,
                scope,
                CancellationToken.None));

        // Then
        Assert.Equal("authorityBaseUrl", exception.ParamName);
    }

    private static byte[] CreatePkcs12(string password)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=MicrosoftCertificateIdentityClientTests",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        return certificate.Export(X509ContentType.Pfx, password);
    }
}
