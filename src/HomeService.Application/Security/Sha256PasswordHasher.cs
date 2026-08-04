using System.Security.Cryptography;

namespace HomeService.Application.Security;

public static class Sha256PasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Algorithm}:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split(':', StringSplitOptions.TrimEntries);
        try
        {
            if (parts.Length == 4
                && string.Equals(parts[0], Algorithm, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[1], out var iterations)
                && iterations is >= 10_000 and <= 1_000_000)
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length < SaltSize || expected.Length < HashSize)
                {
                    return false;
                }

                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }

            return VerifyLegacySha256(password, parts);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static bool NeedsRehash(string passwordHash)
    {
        var parts = passwordHash.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length != 4
            || !string.Equals(parts[0], Algorithm, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(parts[1], out var iterations)
            || iterations < Iterations;
    }

    private static bool VerifyLegacySha256(string password, string[] parts)
    {
        if (parts.Length != 3 || !string.Equals(parts[0], "sha256", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = Convert.FromHexString(parts[2]);
        var actual = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{parts[1]}:{password}"));
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
