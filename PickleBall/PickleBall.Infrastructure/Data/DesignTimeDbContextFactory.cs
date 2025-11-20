using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;

namespace PickleBall.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var searchPaths = new[]
        {
            basePath,
            Path.Combine(basePath, ".."),
            Path.Combine(basePath, "../PickleBall"),
            Path.Combine(basePath, "../../PickleBall")
        }
        .Select(Path.GetFullPath);

        var configPath = searchPaths.FirstOrDefault(path =>
            File.Exists(Path.Combine(path, "appsettings.json"))) ?? basePath;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();

        var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("your-supabase-host"))
        {
            builder.UseNpgsql(connectionString);
        }
        else
        {
            builder.UseInMemoryDatabase("PickleBallShop");
        }

        return new ApplicationDbContext(builder.Options);
    }
}

