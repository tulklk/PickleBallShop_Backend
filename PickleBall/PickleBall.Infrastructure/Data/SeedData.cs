using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Data;

public static class SeedData
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Create admin user if not exists
        if (!context.Users.Any(u => u.Email == "admin@pickleballshop.com"))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@pickleballshop.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                FullName = "Administrator",
                PhoneNumber = "0123456789",
                Role = Role.Admin,
                IsActive = true
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
        }
    }
}
