namespace PickleBall.Application.DTOs.Users;

public class AdminUpdateUserRequest
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Avatar { get; set; }
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
}

