using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Auth;
using PickleBall.Application.Interfaces;
using System.Security.Claims;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

    [EnableRateLimiting("register")]

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration rejected: {Message}", ex.Message);
            return BadRequest(new { message = "Không thể đăng ký với thông tin đã cung cấp" });
        }
    }

    [EnableRateLimiting("password")]

    [HttpPost("verify-email")]
    public async Task<ActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        try
        {
            var result = await _authService.VerifyEmailAsync(request.Token);
            if (result)
            {
                return Ok(new { message = "Email verified successfully" });
            }
            return BadRequest(new { message = "Liên kết xác nhận không hợp lệ hoặc đã hết hạn" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [EnableRateLimiting("password")]

    [HttpPost("resend-verification")]
    public async Task<ActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequest request)
    {
        // Uniform response: never reveal whether the email exists / is already verified.
        try
        {
            await _authService.ResendVerificationEmailAsync(request.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResendVerificationEmail endpoint");
        }

        return Ok(new { message = "Nếu tài khoản tồn tại và chưa được xác nhận, chúng tôi đã gửi lại email xác nhận." });
    }

    [EnableRateLimiting("login")]

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Truncate(Request.Headers["User-Agent"].ToString(), 256);
            var result = await _authService.LoginAsync(request, ipAddress, userAgent);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [EnableRateLimiting("login")]

    [HttpPost("login/google")]
    public async Task<ActionResult<AuthResponse>> LoginWithGoogle([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Truncate(Request.Headers["User-Agent"].ToString(), 256);
            var result = await _authService.LoginWithGoogleAsync(request, ipAddress, userAgent);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [EnableRateLimiting("login")]

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _authService.RefreshTokenAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        try
        {
            var userId = GetUserId();
            var user = await _authService.GetCurrentUserAsync(userId);
            return Ok(user);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout([FromBody] RefreshTokenRequest? request = null)
    {
        try
        {
            var userId = GetUserId();
            
            // If refresh token is provided, revoke it
            if (request != null && !string.IsNullOrEmpty(request.RefreshToken))
            {
                await _authService.LogoutAsync(request.RefreshToken);
            }
            else
            {
                // Otherwise, revoke all refresh tokens for this user
                await _authService.LogoutByUserIdAsync(userId);
            }
            
            return Ok(new { message = "Logged out successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [EnableRateLimiting("password")]

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            await _authService.ForgotPasswordAsync(request.Email);
        }
        catch (Exception ex)
        {
            // Log only; the response must be identical whether or not the account exists / mail failed.
            _logger.LogError(ex, "Error in ForgotPassword endpoint");
        }

        return Ok(new
        {
            message = "Nếu email bạn nhập tồn tại trong hệ thống, chúng tôi đã gửi liên kết đặt lại mật khẩu đến email của bạn. Liên kết có hiệu lực trong 30 phút. Vui lòng kiểm tra hộp thư (bao gồm cả thư mục spam)."
        });
    }

    [EnableRateLimiting("password")]

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}