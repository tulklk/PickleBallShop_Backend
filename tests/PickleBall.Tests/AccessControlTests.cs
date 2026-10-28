using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PickleBall.Domain.Entities;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

/// <summary>Authentication / authorization / BOLA (IDOR) regression tests.</summary>
public class AccessControlTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public AccessControlTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/admin/AdminOrders")]
    [InlineData("/api/admin/AdminUsers")]
    [InlineData("/api/Users")]
    [InlineData("/api/Users/me")]
    [InlineData("/api/Orders/my")]
    [InlineData("/api/Auth/me")]
    public async Task Anonymous_requests_to_protected_endpoints_are_rejected(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_use_admin_endpoints()
    {
        var customer = await _factory.CreateUserAsync();
        var client = await _factory.LoggedInClientAsync(customer);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/AdminOrders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/AdminUsers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/Products", new { name = "x" }, TestHelpers.Json)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_use_admin_endpoints_and_page_size_is_clamped()
    {
        var admin = await _factory.CreateUserAsync(role: Role.Admin);
        var client = await _factory.LoggedInClientAsync(admin);

        var response = await client.GetAsync("/api/admin/AdminOrders?page=-5&pageSize=100000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("100", response.Headers.GetValues("X-Page-Size").Single());
        Assert.Equal("1", response.Headers.GetValues("X-Page").Single());
    }

    [Fact]
    public async Task User_cannot_read_another_users_profile()
    {
        var a = await _factory.CreateUserAsync();
        var b = await _factory.CreateUserAsync();
        var clientA = await _factory.LoggedInClientAsync(a);

        Assert.Equal(HttpStatusCode.Forbidden, (await clientA.GetAsync($"/api/Users/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/Users/{a.Id}")).StatusCode);
    }

    [Fact]
    public async Task User_cannot_read_another_users_order()
    {
        var a = await _factory.CreateUserAsync();
        var b = await _factory.CreateUserAsync();
        var orderOfB = await _factory.CreateOrderAsync(userId: b.Id);

        var clientA = await _factory.LoggedInClientAsync(a);
        var response = await clientA.GetAsync($"/api/Orders/{orderOfB.Id}");
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"expected 404/403 but got {(int)response.StatusCode}");

        var status = await clientA.GetAsync($"/api/Orders/{orderOfB.Id}/status");
        Assert.True(status.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);

        var mine = await clientA.GetAsync("/api/Orders/my");
        Assert.DoesNotContain(orderOfB.OrderNumber, await mine.Content.ReadAsStringAsync());

        var clientB = await _factory.LoggedInClientAsync(b);
        Assert.Equal(HttpStatusCode.OK, (await clientB.GetAsync($"/api/Orders/{orderOfB.Id}")).StatusCode);
    }

    [Fact]
    public async Task Role_cannot_be_escalated_by_mass_assignment()
    {
        var user = await _factory.CreateUserAsync();
        var client = await _factory.LoggedInClientAsync(user);

        var response = await client.PutAsJsonAsync($"/api/Users/{user.Id}",
            new { fullName = "Hacker", role = "Admin", isActive = true, emailVerified = true, id = Guid.NewGuid() },
            TestHelpers.Json);
        Assert.True(response.IsSuccessStatusCode);

        var role = await _factory.WithDb(db => db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.Role).FirstAsync());
        Assert.Equal(Role.Customer, role);

        // Registration ignores a client-supplied role as well
        var email = TestHelpers.UniqueEmail("reg");
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email, password = TestHelpers.StrongPassword, confirmPassword = TestHelpers.StrongPassword,
            fullName = "Reg", role = "Admin"
        }, TestHelpers.Json);
        var registeredRole = await _factory.WithDb(db => db.Users.AsNoTracking().Where(u => u.Email == email).Select(u => u.Role).FirstAsync());
        Assert.Equal(Role.Customer, registeredRole);
    }

    [Fact]
    public async Task Access_token_of_deactivated_user_is_rejected_immediately()
    {
        var user = await _factory.CreateUserAsync();
        var client = await _factory.LoggedInClientAsync(user);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Users/me")).StatusCode);

        await _factory.WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Id == user.Id);
            u.IsActive = false;
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Users/me")).StatusCode);
    }

    [Fact]
    public async Task Access_token_is_rejected_when_the_role_changed_after_issuance()
    {
        var admin = await _factory.CreateUserAsync(role: Role.Admin);
        var client = await _factory.LoggedInClientAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/AdminOrders")).StatusCode);

        await _factory.WithDb(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Id == admin.Id);
            u.Role = Role.Customer;
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/AdminOrders")).StatusCode);
    }

    [Fact]
    public async Task Forged_jwt_signed_with_another_key_is_rejected()
    {
        var user = await _factory.CreateUserAsync(role: Role.Admin);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("attacker-controlled-key-attacker-controlled-key"));
        var token = handler.WriteToken(new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "PickleBallShop", audience: "PickleBallShop",
            claims: new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256)));

        var client = _factory.WithBearer(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/AdminOrders")).StatusCode);

        // alg=none
        var none = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0." +
                   Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{{\"nameid\":\"{user.Id}\",\"role\":\"Admin\"}}")).TrimEnd('=') + ".";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.WithBearer(none).GetAsync("/api/admin/AdminOrders")).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_delete_own_account()
    {
        var admin = await _factory.CreateUserAsync(role: Role.Admin);
        var client = await _factory.LoggedInClientAsync(admin);
        var response = await client.DeleteAsync($"/api/Users/{admin.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
