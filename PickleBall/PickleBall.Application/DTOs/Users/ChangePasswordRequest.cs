namespace PickleBall.Application.DTOs.Users;

public class ChangePasswordRequest
{
    /// <summary>
    /// Current password of the user
    /// </summary>
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// New password (must be at least 8 characters)
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>
    /// Confirmation of the new password (must match NewPassword)
    /// </summary>
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

