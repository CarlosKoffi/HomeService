using System.Security.Cryptography;
using System.Text;

namespace HomeService.Application.Security;

public static class Sha256PasswordHasher
{
    public static string Hash(string password)
    {
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{password}")));
        return $"sha256:{salt}:{hash}";
    }

    public static bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "sha256", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{parts[1]}:{password}")));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(parts[2]));
    }
}
