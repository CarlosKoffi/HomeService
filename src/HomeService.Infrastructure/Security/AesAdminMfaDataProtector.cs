using System.Security.Cryptography;
using System.Text;
using HomeService.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace HomeService.Infrastructure.Security;

public sealed class AesAdminMfaDataProtector(IConfiguration configuration) : IAdminMfaDataProtector
{
    private const string Prefix = "admin-mfa-v1";

    public string Protect(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(ResolveKey(), tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(Prefix));
        return string.Join('.', Prefix, Convert.ToBase64String(nonce), Convert.ToBase64String(ciphertext), Convert.ToBase64String(tag));
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        var parts = protectedValue.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != Prefix)
        {
            throw new CryptographicException("Format du secret Authenticator invalide.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var ciphertext = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(ResolveKey(), tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(Prefix));
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] ResolveKey()
    {
        var configured = configuration["ADMIN_MFA_DATA_PROTECTION_KEY"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "ADMIN_MFA_DATA_PROTECTION_KEY doit contenir une cle aleatoire Base64 de 32 octets.");
        }

        try
        {
            var key = Convert.FromBase64String(configured.Trim());
            return key.Length == 32
                ? key
                : throw new InvalidOperationException(
                    "ADMIN_MFA_DATA_PROTECTION_KEY doit representer exactement 32 octets.");
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "ADMIN_MFA_DATA_PROTECTION_KEY n'est pas une valeur Base64 valide.", exception);
        }
    }
}
