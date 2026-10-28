using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Security;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Services;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Tr1ckyPass", true)]
    [InlineData("Passw0rd", false)] // common password denylist
    [InlineData("Admin@123", false)] // the credential that used to ship in the repository
    [InlineData("abcdefg1", true)]
    [InlineData("short1", false)]
    [InlineData("nodigitsHere", false)]
    [InlineData("1234567890", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Policy(string? password, bool valid) =>
        Assert.Equal(valid, PasswordPolicy.IsValid(password, out _));

    [Fact]
    public void Overlong_passwords_are_rejected() =>
        Assert.False(PasswordPolicy.IsValid(new string('a', 200) + "1", out _));
}

public class CryptoUtilTests
{
    [Fact]
    public void Signature_comparison_is_case_insensitive_and_rejects_empty_values()
    {
        Assert.True(CryptoUtil.SignatureEquals("ABCDEF", "abcdef"));
        Assert.False(CryptoUtil.SignatureEquals("abcdef", "abcdee"));
        Assert.False(CryptoUtil.SignatureEquals("", ""));
        Assert.False(CryptoUtil.SignatureEquals(null, null));
        Assert.False(CryptoUtil.SignatureEquals("abc", "abcd"));
    }

    [Fact]
    public void Generated_tokens_are_unique_url_safe_and_long()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => CryptoUtil.GenerateToken()).ToList();
        Assert.Equal(50, tokens.Distinct().Count());
        Assert.All(tokens, t =>
        {
            Assert.True(t.Length >= 43);
            Assert.Matches("^[A-Za-z0-9_-]+$", t);
        });
    }
}

public class PayOSSignatureTests
{
    private static PayOSService Service(string checksumKey) => new(
        new HttpClient(),
        Options.Create(new PayOSSettings { ChecksumKey = checksumKey }),
        NullLogger<PayOSService>.Instance);

    [Fact]
    public async Task Accepts_a_correctly_signed_webhook()
    {
        var hook = TestHelpers.SignedWebhook(123456, 50_000, checksumKey: "k1");
        Assert.NotNull(await Service("k1").VerifyWebhookAsync(hook));
    }

    [Fact]
    public async Task Rejects_wrong_key_tampering_and_missing_signature()
    {
        var hook = TestHelpers.SignedWebhook(123456, 50_000, checksumKey: "k1");
        Assert.Null(await Service("other-key").VerifyWebhookAsync(hook));

        hook.Data!.Amount = 1;
        Assert.Null(await Service("k1").VerifyWebhookAsync(hook));

        var unsigned = TestHelpers.SignedWebhook(123456, 50_000, checksumKey: "k1");
        unsigned.Signature = "";
        Assert.Null(await Service("k1").VerifyWebhookAsync(unsigned));
    }

    [Fact]
    public async Task Rejects_everything_when_the_checksum_key_is_not_configured()
    {
        // An empty HMAC key is trivially forgeable; the service must refuse instead of "verifying".
        var hook = TestHelpers.SignedWebhook(123456, 50_000, checksumKey: "");
        Assert.Null(await Service("").VerifyWebhookAsync(hook));
    }

    [Fact]
    public async Task Payment_lookup_without_credentials_never_reports_paid()
    {
        Assert.Null(await Service("k1").GetPaymentInfoAsync(123456));
    }
}

public class StartupAndSeedTests
{
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("YOUR_SECRET_KEY_REPLACE_ME_WITH_32_BYTES_OR_MORE")]
    public void Application_refuses_to_start_with_a_missing_weak_or_placeholder_jwt_key(string key)
    {
        using var factory = new ApiFactory(false, key);
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Jwt:SecretKey", ex.ToString());
    }

    private static ApplicationDbContext NewDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase("seed-" + Guid.NewGuid()).Options);

    private static IConfiguration Config(string? email, string? password) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ADMIN_SEED_EMAIL"] = email,
            ["ADMIN_SEED_PASSWORD"] = password
        }).Build();

    [Fact]
    public async Task No_default_admin_is_created_without_explicit_configuration()
    {
        using var db = NewDb();
        await SeedData.SeedAsync(db, Config(null, null), NullLogger.Instance);
        Assert.False(await db.Users.AnyAsync(u => u.Role == Role.Admin));
        Assert.False(await db.Users.AnyAsync(u => u.Email == "admin@pickleballshop.com"));
    }

    [Fact]
    public async Task Weak_seed_password_is_refused()
    {
        using var db = NewDb();
        await SeedData.SeedAsync(db, Config("boss@example.test", "admin123"), NullLogger.Instance);
        await SeedData.SeedAsync(db, Config("boss@example.test", "Admin@123"), NullLogger.Instance);
        Assert.False(await db.Users.AnyAsync(u => u.Role == Role.Admin));
    }

    [Fact]
    public async Task Admin_is_created_from_environment_only_once_with_a_hashed_password()
    {
        using var db = NewDb();
        await SeedData.SeedAsync(db, Config("Boss@Example.Test", "Sup3rSecretPass"), NullLogger.Instance);
        await SeedData.SeedAsync(db, Config("second@example.test", "Sup3rSecretPass"), NullLogger.Instance);

        var admin = Assert.Single(await db.Users.Where(u => u.Role == Role.Admin).ToListAsync());
        Assert.Equal("boss@example.test", admin.Email);
        Assert.NotEqual("Sup3rSecretPass", admin.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Sup3rSecretPass", admin.PasswordHash));
    }
}

