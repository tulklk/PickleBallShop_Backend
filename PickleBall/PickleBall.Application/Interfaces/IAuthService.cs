using PickleBall.Application.DTOs.Auth;

namespace PickleBall.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null);
    Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request, string? ipAddress = null, string? userAgent = null);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
    Task LogoutAsync(string? refreshToken = null);
    Task LogoutByUserIdAsync(Guid userId);
    Task LogoutAllAsync(Guid userId);
}
