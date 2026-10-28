using PickleBall.Application.Mapping;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Auth;
using PickleBall.Application.Interfaces;
using PickleBall.Application.Security;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using System.Security.Cryptography;

namespace PickleBall.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ApplicationDbContext context, ITokenService tokenService, IMapper mapper, IConfiguration configuration, IEmailService emailService, ILogger<AuthService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _emailService = emailService;
        _logger = logger;
    }

    // Used to equalise login timing when the account does not exist (prevents user enumeration by timing).
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("timing-equalisation-only-Passw0rd");

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private string GetFrontendBaseUrl()
    {
        var url = _configuration["Email:FrontendUrl"] ?? _configuration["FRONTEND_URL"];
        if (!string.IsNullOrWhiteSpace(url) &&
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        throw new InvalidOperationException("Email:FrontendUrl is not configured");
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var email = NormalizeEmail(request.Email);

        // Hash first so the "email exists" path costs the same as the "new account" path.
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var response = new RegisterResponse
        {
            Message = "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản.",
            Email = email
        };

        // Do not reveal whether the email is already registered (account enumeration): same response either way.
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
        {
            _logger.LogInformation("Registration attempted for an already registered email");
            return response;
        }

        var user = new User
        {
            Email = email,
            PasswordHash = passwordHash,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = Role.Customer, // role is never taken from client input
            IsActive = true,
            EmailVerified = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await IssueAndSendVerificationAsync(user);

        return response;
    }

    private async Task IssueAndSendVerificationAsync(User user)
    {
        var verificationToken = CryptoUtil.GenerateToken(32);
        _context.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            Token = verificationToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false
        });
        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, verificationToken);
        }
        catch (Exception ex)
        {
            // Never surface SMTP details to the caller; the user can request a resend.
            _logger.LogError(ex, "Failed to send verification email for user {UserId}", user.Id);
        }
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var verificationToken = await _context.EmailVerificationTokens
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Token == token);

        if (verificationToken == null || verificationToken.IsUsed || verificationToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Liên kết xác nhận không hợp lệ hoặc đã hết hạn");
        }

        verificationToken.IsUsed = true;
        // Verifying an email must never re-activate an account an admin deactivated.
        verificationToken.User.EmailVerified = true;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);

        // Always report success to the caller (no account enumeration); only act when it makes sense.
        if (user == null || !user.IsActive || user.Role == Role.Admin || user.EmailVerified)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        var existingTokens = await _context.EmailVerificationTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed)
            .ToListAsync();

        // Throttle: a token issued in the last minute already exists (expiry is 24h after issuance).
        if (existingTokens.Any(t => t.ExpiresAt > now.AddHours(24).AddMinutes(-1)))
        {
            return true;
        }

        foreach (var token in existingTokens)
        {
            token.IsUsed = true;
        }

        await IssueAndSendVerificationAsync(user);
        return true;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyPasswordHash);
        if (user == null || !passwordOk)
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Account is inactive");
        }

        // Admin accounts don't need email verification
        if (user.Role != Role.Admin && !user.EmailVerified)
        {
            throw new UnauthorizedAccessException("Please verify your email before logging in");
        }

        return await BuildAuthResponseAsync(user, ipAddress, userAgent);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, string? ipAddress, string? userAgent)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        await _tokenService.SaveRefreshTokenAsync(user.Id, refreshToken, ipAddress, userAgent);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = _mapper.Map<UserDto>(user)
        };
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request, string? ipAddress = null, string? userAgent = null)
    {
        var googleSettings = _configuration.GetSection("Google");
        var clientId = googleSettings["ClientId"];

        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogError("Google ClientId is not configured");
            throw new UnauthorizedAccessException("Google login is not available");
        }

        GoogleJsonWebSignature.Payload? payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("Rejected invalid Google token: {Message}", ex.Message);
            throw new UnauthorizedAccessException("Invalid Google token");
        }

        if (payload == null)
        {
            throw new UnauthorizedAccessException("Failed to verify Google token");
        }

        var googleId = payload.Subject;
        var email = NormalizeEmail(payload.Email);
        var fullName = (payload.Name ?? $"{payload.GivenName} {payload.FamilyName}").Trim();

        if (string.IsNullOrEmpty(email))
        {
            throw new UnauthorizedAccessException("Email not provided by Google");
        }

        // Linking/creating an account by email is only safe when Google asserts the email is verified;
        // otherwise anyone could take over an existing account by registering the victim's email at Google.
        if (!payload.EmailVerified)
        {
            throw new UnauthorizedAccessException("Google email is not verified");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = email;
        }

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.GoogleId == googleId || u.Email.ToLower() == email);

        if (user == null)
        {
            user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(CryptoUtil.GenerateToken(32)), // unguessable; user can use "forgot password" to set one
                FullName = fullName,
                GoogleId = googleId,
                Role = Role.Customer,
                IsActive = true,
                EmailVerified = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is inactive");
            }

            // Never link a different Google identity to an account that is already linked.
            if (!string.IsNullOrEmpty(user.GoogleId) && user.GoogleId != googleId)
            {
                throw new UnauthorizedAccessException("Invalid Google token");
            }

            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = googleId;
            }

            if (!user.EmailVerified)
            {
                user.EmailVerified = true;
            }

            await _context.SaveChangesAsync();
        }

        return await BuildAuthResponseAsync(user, ipAddress, userAgent);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var refreshToken = string.IsNullOrWhiteSpace(request.RefreshToken)
            ? null
            : await _tokenService.GetRefreshTokenAsync(request.RefreshToken);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var user = refreshToken.User;

        // A deactivated account must not be able to mint new tokens.
        if (user == null || !user.IsActive)
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Rotate: revoke old token, issue new pair
        refreshToken.RevokedAt = DateTime.UtcNow;

        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        await _tokenService.SaveRefreshTokenAsync(user.Id, newRefreshToken, refreshToken.IpAddress, refreshToken.UserAgent);

        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = _mapper.Map<UserDto>(user)
        };
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Account is inactive");
        }

        return _mapper.Map<UserDto>(user);
    }

    public async Task LogoutAsync(string? refreshToken = null)
    {
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
        }
    }

    public async Task LogoutByUserIdAsync(Guid userId)
    {
        await _tokenService.RevokeAllRefreshTokensAsync(userId);
    }

    public async Task LogoutAllAsync(Guid userId)
    {
        await _tokenService.RevokeAllRefreshTokensAsync(userId);
    }

    private static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(30);

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        var normalized = NormalizeEmail(email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);

        // Same observable behaviour for unknown / inactive accounts (no enumeration).
        if (user == null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for unknown or inactive account");
            return true;
        }

        var now = DateTime.UtcNow;

        // Throttle: a link was already issued in the last minute.
        if (user.PasswordResetExpiresAt.HasValue &&
            user.PasswordResetExpiresAt.Value > now.Add(PasswordResetLifetime).AddMinutes(-1))
        {
            return true;
        }

        var token = CryptoUtil.GenerateToken(32);
        user.PasswordResetTokenHash = CryptoUtil.Sha256Hex(token);
        user.PasswordResetExpiresAt = now.Add(PasswordResetLifetime);
        await _context.SaveChangesAsync();

        try
        {
            var resetUrl = $"{GetFrontendBaseUrl()}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";
            await _emailService.SendPasswordResetLinkEmailAsync(user.Email, user.FullName, resetUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email for user {UserId}", user.Id);
        }

        return true;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        const string invalid = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn";

        if (!PasswordPolicy.IsValid(request.NewPassword, out _))
        {
            throw new InvalidOperationException(PasswordPolicy.RequirementsMessage);
        }

        var normalized = NormalizeEmail(request.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);

        if (user == null || !user.IsActive ||
            string.IsNullOrEmpty(user.PasswordResetTokenHash) ||
            !user.PasswordResetExpiresAt.HasValue ||
            user.PasswordResetExpiresAt.Value < DateTime.UtcNow ||
            !CryptoUtil.SignatureEquals(user.PasswordResetTokenHash, CryptoUtil.Sha256Hex(request.Token ?? string.Empty)))
        {
            throw new InvalidOperationException(invalid);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        // Single use
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresAt = null;
        // Reaching this point proves control of the mailbox.
        user.EmailVerified = true;
        await _context.SaveChangesAsync();

        // Invalidate every existing session.
        await _tokenService.RevokeAllRefreshTokensAsync(user.Id);
    }
}