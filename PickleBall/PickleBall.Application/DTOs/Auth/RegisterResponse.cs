namespace PickleBall.Application.DTOs.Auth;

public class RegisterResponse
{
    public string Message { get; set; } = "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản.";
    public string Email { get; set; } = string.Empty;
}

