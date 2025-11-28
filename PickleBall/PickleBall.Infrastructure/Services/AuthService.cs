using AutoMapper;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PickleBall.Application.DTOs.Auth;
using PickleBall.Application.Interfaces;
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

    public AuthService(ApplicationDbContext context, ITokenService tokenService, IMapper mapper, IConfiguration configuration, IEmailService emailService)
    {
        _context = context;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _emailService = emailService;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if user already exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("Email already exists");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user (not verified yet)
        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = Role.Customer,
            IsActive = true,
            EmailVerified = false // Email not verified yet
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Generate email verification token
        var verificationToken = GenerateVerificationToken();
        var verificationTokenEntity = new EmailVerificationToken
        {
            UserId = user.Id,
            Token = verificationToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24), // Token expires in 24 hours
            IsUsed = false
        };

        _context.EmailVerificationTokens.Add(verificationTokenEntity);
        await _context.SaveChangesAsync();

        // Send verification email
        await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, verificationToken);

        return new RegisterResponse
        {
            Message = "Đăng ký thành công! Vui lòng kiểm tra email để xác nhận tài khoản.",
            Email = user.Email
        };
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var verificationToken = await _context.EmailVerificationTokens
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Token == token);

        if (verificationToken == null)
        {
            throw new InvalidOperationException("Invalid verification token");
        }

        if (verificationToken.IsUsed)
        {
            throw new InvalidOperationException("Verification token has already been used");
        }

        if (verificationToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Verification token has expired");
        }

        // Mark token as used
        verificationToken.IsUsed = true;

        // Verify user email
        verificationToken.User.EmailVerified = true;
        verificationToken.User.IsActive = true; // Activate account upon verification

        await _context.SaveChangesAsync();

        return true;
    }

    private string GenerateVerificationToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Account is inactive");
        }

        if (!user.EmailVerified)
        {
            throw new UnauthorizedAccessException("Please verify your email before logging in");
        }

        // Generate tokens
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
            throw new InvalidOperationException("Google ClientId is not configured");
        }

        // Verify Google ID token
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
            throw new UnauthorizedAccessException("Invalid Google token: " + ex.Message);
        }

        if (payload == null)
        {
            throw new UnauthorizedAccessException("Failed to verify Google token");
        }

        // Extract user information from Google payload
        var googleId = payload.Subject;
        var email = payload.Email;
        var fullName = payload.Name ?? payload.GivenName + " " + payload.FamilyName;

        if (string.IsNullOrEmpty(email))
        {
            throw new UnauthorizedAccessException("Email not provided by Google");
        }

        // Find existing user by GoogleId or Email
        var user = await _context.Users.FirstOrDefaultAsync(u => 
            u.GoogleId == googleId || u.Email == email);

        if (user == null)
        {
            // Create new user (Google users are automatically verified)
            user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Random password for Google users
                FullName = fullName.Trim(),
                GoogleId = googleId,
                Role = Role.Customer,
                IsActive = true,
                EmailVerified = true // Google login users are automatically verified
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            // Update existing user if GoogleId is missing
            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = googleId;
            }

            // If existing user doesn't have email verified, verify it now (Google login)
            if (!user.EmailVerified)
            {
                user.EmailVerified = true;
            }

            // Update name if different
            if (user.FullName != fullName.Trim())
            {
                user.FullName = fullName.Trim();
            }

            await _context.SaveChangesAsync();

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is inactive");
            }
        }

        // Generate tokens
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

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var refreshToken = await _tokenService.GetRefreshTokenAsync(request.RefreshToken);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var user = refreshToken.User;

        // Revoke old token
        refreshToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        await _tokenService.SaveRefreshTokenAsync(user.Id, newRefreshToken);

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
}
