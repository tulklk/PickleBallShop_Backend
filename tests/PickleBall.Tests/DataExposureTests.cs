using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

public class DataExposureTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public DataExposureTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_review_listing_never_exposes_reviewer_phone_or_email()
    {
        var product = await _factory.CreateProductAsync();
        var client = _factory.CreateClient();
        const string phone = "0987654321";
        var email = TestHelpers.UniqueEmail("reviewer");

        var create = await client.PostAsJsonAsync($"/api/products/{product.Id}/reviews", new
        {
            fullName = "Reviewer",
            phoneNumber = phone,
            email,
            rating = 5,
            comment = "Great paddle, very happy with it"
        }, TestHelpers.Json);
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.DoesNotContain(phone, createBody);
        Assert.DoesNotContain(email, createBody);

        // data is still collected for the shop...
        var stored = await _factory.WithDb(db => db.ProductReviews.AsNoTracking().Where(r => r.ProductId == product.Id).ToListAsync());
        Assert.Equal(phone, Assert.Single(stored).PhoneNumber);

        // ...but never returned publicly
        foreach (var url in new[] { $"/api/products/{product.Id}/reviews", $"/api/products/{product.Slug}/reviews" })
        {
            var list = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var body = await list.Content.ReadAsStringAsync();
            Assert.Contains("Reviewer", body);
            Assert.DoesNotContain(phone, body);
            Assert.DoesNotContain(email, body);
            Assert.DoesNotContain("phoneNumber", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task User_payload_never_contains_password_hash_or_reset_token()
    {
        var user = await _factory.CreateUserAsync();
        var client = await _factory.LoggedInClientAsync(user);
        var me = await client.GetStringAsync("/api/Users/me");
        Assert.DoesNotContain("passwordHash", me, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resetToken", me, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(user.PasswordHash, me);

        var authMe = await client.GetStringAsync("/api/auth/me");
        Assert.DoesNotContain("passwordHash", authMe, StringComparison.OrdinalIgnoreCase);

        var admin = await _factory.CreateUserAsync(role: PickleBall.Domain.Entities.Role.Admin);
        var adminClient = await _factory.LoggedInClientAsync(admin);
        var list = await adminClient.GetStringAsync("/api/admin/AdminUsers?pageSize=100");
        Assert.DoesNotContain("passwordHash", list, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", list, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Profile_update_rejects_dangerous_avatar_urls()
    {
        var user = await _factory.CreateUserAsync();
        var client = await _factory.LoggedInClientAsync(user);

        var js = await client.PutAsJsonAsync($"/api/Users/{user.Id}",
            new { fullName = "Name", avatar = "javascript:alert(1)" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, js.StatusCode);

        var ok = await client.PutAsJsonAsync($"/api/Users/{user.Id}",
            new { fullName = "Name", avatar = "https://cdn.example.test/a.png" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Review_image_url_must_be_http_or_https()
    {
        var product = await _factory.CreateProductAsync();
        var response = await _factory.CreateClient().PostAsJsonAsync($"/api/products/{product.Id}/reviews", new
        {
            fullName = "R", phoneNumber = "0987654321", rating = 4,
            comment = "A perfectly fine review text", reviewImageUrl = "javascript:alert(1)"
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
