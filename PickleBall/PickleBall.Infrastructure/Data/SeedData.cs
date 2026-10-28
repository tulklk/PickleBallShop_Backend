using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickleBall.Application.Security;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data;

public static class SeedData
{
    /// <summary>
    /// Bootstraps the first administrator ONLY when ADMIN_SEED_EMAIL and ADMIN_SEED_PASSWORD are supplied
    /// through the environment and no admin exists yet. There is intentionally no default credential.
    /// </summary>
    public static async Task SeedAsync(ApplicationDbContext context, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["ADMIN_SEED_EMAIL"]?.Trim();
        var password = configuration["ADMIN_SEED_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation("Admin seeding skipped (ADMIN_SEED_EMAIL / ADMIN_SEED_PASSWORD not set).");
            return;
        }

        if (await context.Users.AnyAsync(u => u.Role == Role.Admin))
        {
            logger.LogInformation("Admin seeding skipped: an administrator already exists.");
            return;
        }

        if (!PasswordPolicy.IsValid(password, out var reason))
        {
            logger.LogError("Admin seeding skipped: ADMIN_SEED_PASSWORD rejected ({Reason}).", reason);
            return;
        }

        var normalizedEmail = email.ToLowerInvariant();
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (existing != null)
        {
            logger.LogWarning("Admin seeding skipped: user with the configured email already exists (not promoting automatically).");
            return;
        }

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = "Administrator",
            Role = Role.Admin,
            IsActive = true,
            EmailVerified = true
        });

        await context.SaveChangesAsync();
        logger.LogWarning("Initial administrator created for {Email}. Remove ADMIN_SEED_* secrets now.", normalizedEmail);
    }
}
