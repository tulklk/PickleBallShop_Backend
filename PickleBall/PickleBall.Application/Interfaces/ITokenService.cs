using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Task<RefreshToken> SaveRefreshTokenAsync(Guid userId, string refreshToken, string? ipAddress = null, string? userAgent = null);
    Task<RefreshToken?> GetRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token);
    Task RevokeAllRefreshTokensAsync(Guid userId);
}
