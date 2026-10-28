namespace PickleBall.Application.Security;

/// <summary>
/// Single source of truth for password strength rules (registration, change, reset, admin seed).
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128; // BCrypt only uses the first 72 bytes; cap input to avoid abuse.

    public const string RequirementsMessage =
        "Mật khẩu phải có từ 8 đến 128 ký tự, gồm ít nhất 1 chữ cái và 1 chữ số.";

    // Known default / very common passwords (includes the credentials that used to ship in the repository).
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin123", "admin@123", "admin1234", "admin@1234", "password1", "password123", "password@123",
        "pa55word1", "qwerty123", "qwerty12345", "abc12345", "abcd1234", "12345678a", "a12345678",
        "iloveyou1", "welcome123", "letmein123", "pickleball123", "pickleball@123", "user1234", "test1234",
        "changeme123", "p@ssw0rd", "p@ssw0rd1", "passw0rd", "passw0rd1"
    };

    public static bool IsValid(string? password, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrEmpty(password))
        {
            reason = "required";
            return false;
        }

        if (password.Length < MinLength || password.Length > MaxLength)
        {
            reason = "length";
            return false;
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            reason = "complexity";
            return false;
        }

        if (CommonPasswords.Contains(password))
        {
            reason = "common";
            return false;
        }

        return true;
    }
}
