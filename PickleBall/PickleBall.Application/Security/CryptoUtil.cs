using System.Security.Cryptography;
using System.Text;

namespace PickleBall.Application.Security;

public static class CryptoUtil
{
    /// <summary>Constant-time, case-insensitive comparison of two hex/ASCII signatures.</summary>
    public static bool SignatureEquals(string? expected, string? actual)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(expected.ToLowerInvariant());
        var b = Encoding.UTF8.GetBytes(actual.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>Lowercase hex SHA-256 of a UTF-8 string. Used to store tokens at rest.</summary>
    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>URL-safe random token with the given number of random bytes.</summary>
    public static string GenerateToken(int byteCount = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
