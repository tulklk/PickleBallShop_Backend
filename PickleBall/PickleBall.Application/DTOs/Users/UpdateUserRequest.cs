namespace PickleBall.Application.DTOs.Users;

public class UpdateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

