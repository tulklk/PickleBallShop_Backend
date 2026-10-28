using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Auth;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

public class AuthSecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public AuthSecurityTests(ApiFactory factory) => _factory = factory;

    private static async Task<string> MessageOf(HttpResponseMessage r)
    {
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
    }

    [Fact]
    public async Task Debug_test_email_endpoint_no_longer_exists()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/test-email",
            new { email = "victim@example.test", fullName = "x" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(_factory.Email.Sent, s => s.Contains("victim@example.test"));
    }

    [Fact]
    public async Task Debug_test_email_for_orders_and_chat_test_endpoints_are_gone()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/chat/test", null)).StatusCode);
        var user = await _factory.CreateUserAsync();
        var authed = await _factory.LoggedInClientAsync(user);
        var order = await _factory.CreateOrderAsync(userId: user.Id);
        var r = await authed.PostAsync($"/api/Orders/{order.Id}/test-email", null);
        Assert.True(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("abcdefgh")]      // no digit
    [InlineData("12345678")]      // no letter
    [InlineData("Ab1")]           // too short
    public async Task Registration_rejects_weak_passwords(string password)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email = TestHelpers.UniqueEmail(), password, confirmPassword = password, fullName = "Weak"
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registration_does_not_reveal_whether_the_email_exists()
    {
        var existing = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        object Body(string email) => new
        {
            email, password = TestHelpers.StrongPassword, confirmPassword = TestHelpers.StrongPassword, fullName = "Dup"
        };

        var fresh = await client.PostAsJsonAsync("/api/auth/register", Body(TestHelpers.UniqueEmail()), TestHelpers.Json);
        var duplicate = await client.PostAsJsonAsync("/api/auth/register", Body(existing.Email), TestHelpers.Json);
        var duplicateUpper = await client.PostAsJsonAsync("/api/auth/register", Body(existing.Email.ToUpperInvariant()), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateUpper.StatusCode);
        Assert.Equal(await MessageOf(fresh), await MessageOf(duplicate));

        var count = await _factory.WithDb(db => db.Users.CountAsync(u => u.Email.ToLower() == existing.Email));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Login_failure_is_identical_for_unknown_user_and_wrong_password()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Wr0ngPassword" }, TestHelpers.Json);
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = TestHelpers.UniqueEmail(), password = "Wr0ngPassword" }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await MessageOf(wrong), await MessageOf(unknown));
    }

    [Fact]
    public async Task Refresh_tokens_are_stored_hashed_rotate_and_cannot_be_replayed()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();
        var login = await client.LoginAsync(user.Email);

        var stored = await _factory.WithDb(db => db.RefreshTokens.AsNoTracking().Where(t => t.UserId == user.Id).Select(t => t.Token).ToListAsync());
        Assert.DoesNotContain(login.RefreshToken, stored);
        Assert.All(stored, t => Assert.Matches("^[0-9a-f]{64}$", t));

        var first = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_is_refused_for_a_deactivated_account()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();
        var login = await client.LoginAsync(user.Email);

        await _factory.WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Id == user.Id);
            u.IsActive = false;
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static (string Token, string Email) ParseLink(string link)
    {
        var uri = new Uri(link);
        var query = HttpUtility.ParseQueryString(uri.Query);
        return (query["token"]!, query["email"]!);
    }

    [Fact]
    public async Task Forgot_password_never_emails_a_password_and_does_not_enumerate_accounts()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email }, TestHelpers.Json);
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = TestHelpers.UniqueEmail() }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(await MessageOf(known), await MessageOf(unknown));

        var link = Assert.Single(_factory.Email.ResetLinks, l => l.To == user.Email).Link;
        Assert.StartsWith("https://pickleball-shop-frontend.vercel.app/reset-password?token=", link);

        // The old password still works: nothing was changed by merely requesting a reset.
        await client.LoginAsync(user.Email);

        // Only a hash of the token is stored
        var (token, _) = ParseLink(link);
        var stored = await _factory.WithDb(db => db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.PasswordResetTokenHash).FirstAsync());
        Assert.NotNull(stored);
        Assert.NotEqual(token, stored);
        Assert.Matches("^[0-9a-f]{64}$", stored!);
    }

    [Fact]
    public async Task Password_reset_token_is_single_use_revokes_sessions_and_enforces_policy()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();
        var oldSession = await client.LoginAsync(user.Email);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email }, TestHelpers.Json);
        var (token, email) = ParseLink(Assert.Single(_factory.Email.ResetLinks, l => l.To == user.Email).Link);

        // weak password refused, token stays valid
        var weak = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword = "short", confirmNewPassword = "short" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        // wrong token refused
        var wrong = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token = "not-the-token", newPassword = "N3wPassword99", confirmNewPassword = "N3wPassword99" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        // wrong email with right token refused
        var otherUser = await _factory.CreateUserAsync();
        var wrongEmail = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = otherUser.Email, token, newPassword = "N3wPassword99", confirmNewPassword = "N3wPassword99" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrongEmail.StatusCode);

        var ok = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword = "N3wPassword99", confirmNewPassword = "N3wPassword99" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // single use
        var reuse = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword = "An0therPassword1", confirmNewPassword = "An0therPassword1" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        // old password dead, new works
        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = TestHelpers.StrongPassword }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await client.LoginAsync(user.Email, "N3wPassword99");

        // sessions issued before the reset are revoked
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = oldSession.RefreshToken }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Expired_password_reset_token_is_rejected()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = user.Email }, TestHelpers.Json);
        var (token, email) = ParseLink(Assert.Single(_factory.Email.ResetLinks, l => l.To == user.Email).Link);

        await _factory.WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Id == user.Id);
            u.PasswordResetExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword = "N3wPassword99", confirmNewPassword = "N3wPassword99" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Changing_password_revokes_existing_refresh_tokens()
    {
        var user = await _factory.CreateUserAsync();
        var client = _factory.CreateClient();
        var session = await client.LoginAsync(user.Email);
        var authed = _factory.WithBearer(session.AccessToken);

        var response = await authed.PostAsJsonAsync("/api/Users/me/change-password", new
        {
            currentPassword = TestHelpers.StrongPassword,
            newPassword = "Ch4ngedPassword1",
            confirmNewPassword = "Ch4ngedPassword1"
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Resend_verification_does_not_reveal_account_state()
    {
        var verified = await _factory.CreateUserAsync(verified: true);
        var client = _factory.CreateClient();

        var a = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email = verified.Email }, TestHelpers.Json);
        var b = await client.PostAsJsonAsync("/api/auth/resend-verification", new { email = TestHelpers.UniqueEmail() }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        Assert.Equal(await MessageOf(a), await MessageOf(b));
    }

    [Fact]
    public async Task Verifying_email_does_not_reactivate_a_deactivated_account()
    {
        var user = await _factory.CreateUserAsync(active: false, verified: false);
        var token = "verify-" + Guid.NewGuid().ToString("N");
        await _factory.WithDb(async db =>
        {
            db.EmailVerificationTokens.Add(new PickleBall.Domain.Entities.EmailVerificationToken
            {
                UserId = user.Id, Token = token, ExpiresAt = DateTime.UtcNow.AddHours(1), IsUsed = false
            });
            await db.SaveChangesAsync();
        });

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/verify-email", new { token }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var active = await _factory.WithDb(db => db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.IsActive).FirstAsync());
        Assert.False(active);
    }
}

