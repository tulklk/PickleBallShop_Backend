using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IEmailService emailService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _emailService = emailService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
    }

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
            return BadRequest(new { message = ex.Message });
        }
    }

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
            return BadRequest(new { message = "Failed to verify email" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("resend-verification")]
    public async Task<ActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequest request)
    {
        try
        {
            var result = await _authService.ResendVerificationEmailAsync(request.Email);
            if (result)
            {
                return Ok(new { message = "Verification email sent successfully" });
            }
            return BadRequest(new { message = "Failed to send verification email" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var result = await _authService.LoginAsync(request, ipAddress, userAgent);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("login/google")]
    public async Task<ActionResult<AuthResponse>> LoginWithGoogle([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].ToString();
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

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email là bắt buộc" });
            }

            var result = await _authService.ForgotPasswordAsync(request.Email);
            if (result)
            {
                // Always return success message to prevent email enumeration
                return Ok(new 
                { 
                    message = "Nếu email bạn nhập tồn tại trong hệ thống, chúng tôi đã gửi mật khẩu mới đến email của bạn. Vui lòng kiểm tra hộp thư (bao gồm cả thư mục spam)."
                });
            }
            return BadRequest(new { message = "Không thể xử lý yêu cầu khôi phục mật khẩu" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ForgotPassword endpoint for email: {Email}", request.Email);
            return StatusCode(500, new 
            { 
                message = "Đã xảy ra lỗi khi xử lý yêu cầu khôi phục mật khẩu",
                error = ex.Message
            });
        }
    }

    [HttpPost("test-email")]
    public async Task<ActionResult> TestEmail([FromBody] TestEmailRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            // Generate a test verification token
            var testToken = Guid.NewGuid().ToString().Replace("-", "");
            
            // Send test verification email
            await _emailService.SendVerificationEmailAsync(
                request.Email, 
                request.FullName, 
                testToken
            );
            
            return Ok(new 
            { 
                message = "Test email sent successfully",
                email = request.Email,
                fullName = request.FullName,
                testToken = testToken,
                note = "This is a test email. The token generated here is for testing purposes only."
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new 
            { 
                message = "Failed to send test email",
                error = ex.Message,
                details = ex.InnerException?.Message
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new 
            { 
                message = "Failed to send test email",
                error = ex.Message,
                details = ex.InnerException?.Message,
                type = ex.GetType().Name
            });
        }
    }
}
