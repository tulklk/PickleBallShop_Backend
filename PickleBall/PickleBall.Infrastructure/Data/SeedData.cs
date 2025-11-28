using Microsoft.EntityFrameworkCore;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data;

public static class SeedData
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Create admin user if not exists, or update existing admin to ensure EmailVerified = true
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@pickleballshop.com");
        
        if (adminUser == null)
        {
            adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@pickleballshop.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FullName = "Administrator",
                PhoneNumber = "0123456789",
                Role = Role.Admin,
                IsActive = true,
                EmailVerified = true // Admin accounts don't need email verification
            };

            context.Users.Add(adminUser);
        }
        else
        {
            // Update existing admin to ensure EmailVerified = true
            if (!adminUser.EmailVerified)
            {
                adminUser.EmailVerified = true;
            }
        }
        
        await context.SaveChangesAsync();
    }
}
