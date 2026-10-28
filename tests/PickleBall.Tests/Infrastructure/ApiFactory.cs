using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;

// Tests mutate process-wide environment variables (configuration) and share one in-memory database.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PickleBall.Tests.Infrastructure;

/// <summary>Captures outgoing mail instead of using SMTP.</summary>
public sealed class FakeEmailService : IEmailService
{
    public ConcurrentQueue<(string To, string Link)> ResetLinks { get; } = new();
    public ConcurrentQueue<string> Sent { get; } = new();

    public Task SendVerificationEmailAsync(string email, string fullName, string verificationToken)
    {
        Sent.Enqueue($"verify:{email}");
        return Task.CompletedTask;
    }

    public Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        Sent.Enqueue($"mail:{toEmail}");
        return Task.FromResult(true);
    }

    public Task SendOrderConfirmationEmailAsync(Order order, string? clientIpAddress = null)
    {
        Sent.Enqueue($"order:{order.Id}");
        return Task.CompletedTask;
    }

    public Task SendGuestOrderConfirmationEmailAsync(Order order, string? clientIpAddress = null)
    {
        Sent.Enqueue($"guest-order:{order.Id}");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkEmailAsync(string email, string fullName, string resetUrl)
    {
        ResetLinks.Enqueue((email, resetUrl));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Boots the real API (real pipeline, real controllers/validators/services) on the EF InMemory database.
/// Configuration that Program.cs reads before the host is built is passed through environment variables.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string DefaultJwtKey = "unit-test-signing-key-which-is-long-enough-0123456789";
    public const string PayOSChecksumKey = "test-payos-checksum-key";

    public FakeEmailService Email { get; } = new();

    public ApiFactory() : this(false, null)
    {
    }

    internal ApiFactory(bool rateLimiting, string? jwtKey = null, string? connectionString = null)
    {
        Set("ASPNETCORE_ENVIRONMENT", "Production");
        Set("ConnectionStrings__DefaultConnection", connectionString ?? "");
        Set("Jwt__SecretKey", jwtKey ?? DefaultJwtKey);
        Set("Security__RateLimiting__Enabled", rateLimiting ? "true" : "false");
        Set("Security__AuthStateCacheSeconds", "0");
        Set("Payment__PayOS__ChecksumKey", PayOSChecksumKey);
        Set("Payment__PayOS__ClientId", "");
        Set("Payment__PayOS__ApiKey", "");
        Set("Google__Gemini__ApiKey", "");
        Set("ADMIN_SEED_EMAIL", "");
        Set("ADMIN_SEED_PASSWORD", "");
    }

    private static void Set(string key, string value) => Environment.SetEnvironmentVariable(key, value);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Email);

            // Test-only controllers (simulate unexpected failures to verify error responses do not leak internals)
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);

            // Kestrel always supplies a remote IP; TestServer does not. Use a public (untrusted) address so
            // forwarded-header trust behaves like a direct internet client.
            services.AddTransient<IStartupFilter, FakePublicRemoteIpFilter>();
        });
    }

    private sealed class FakePublicRemoteIpFilter : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
            Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nxt) =>
            {
                context.Connection.RemoteIpAddress ??= System.Net.IPAddress.Parse("198.51.100.23");
                await nxt();
            });
            next(app);
        };
    }
}
