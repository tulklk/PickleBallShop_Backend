using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PickleBall.Application.DTOs.Auth;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Tests.Infrastructure;

/// <summary>Throws on purpose so tests can verify the global error handler never leaks internals.</summary>
[ApiController]
[Route("api/__test")]
public class BoomController : ControllerBase
{
    [HttpGet("boom")]
    public IActionResult Boom() =>
        throw new Exception("Host=db.internal;Password=hunter2 at Npgsql.SomeInternalClass");

    [HttpGet("boom-framework")]
    public IActionResult BoomFramework() =>
        Ok(new List<int>().First()); // InvalidOperationException from System.Linq: "Sequence contains no elements"
}

public static class TestHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public const string StrongPassword = "Str0ngPassw0rd";

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@example.test";

    public static HttpClient NoRedirectClient(this ApiFactory factory) =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static async Task<T> WithDb<T>(this ApiFactory factory, Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public static Task WithDb(this ApiFactory factory, Func<ApplicationDbContext, Task> action) =>
        factory.WithDb<int>(async db => { await action(db); return 0; });

    public static async Task<User> CreateUserAsync(
        this ApiFactory factory, string? email = null, string password = StrongPassword,
        Role role = Role.Customer, bool active = true, bool verified = true)
    {
        var user = new User
        {
            Email = (email ?? UniqueEmail()).ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 4),
            FullName = "Test User",
            Role = role,
            IsActive = active,
            EmailVerified = verified
        };
        await factory.WithDb(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        return user;
    }

    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email, string password = StrongPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    public static HttpClient WithBearer(this ApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<HttpClient> LoggedInClientAsync(this ApiFactory factory, User user)
    {
        var login = await factory.CreateClient().LoginAsync(user.Email);
        return factory.WithBearer(login.AccessToken);
    }

    public static async Task<Product> CreateProductAsync(
        this ApiFactory factory, decimal price = 100_000m, int stock = 10, bool active = true)
    {
        var product = new Product
        {
            Name = "Test Paddle " + Guid.NewGuid().ToString("N")[..6],
            Slug = "test-paddle-" + Guid.NewGuid().ToString("N")[..10],
            Price = price,
            StockQuantity = stock,
            IsActive = active
        };
        await factory.WithDb(async db =>
        {
            var category = new ProductCategory { Name = "Cat " + Guid.NewGuid().ToString("N")[..6], Slug = "cat-" + Guid.NewGuid().ToString("N")[..10] };
            db.ProductCategories.Add(category);
            product.CategoryId = category.Id;
            db.Products.Add(product);
            await db.SaveChangesAsync();
        });
        return product;
    }

    public static async Task<int> StockOfAsync(this ApiFactory factory, Guid productId) =>
        await factory.WithDb(db => db.Products.AsNoTracking().Where(p => p.Id == productId).Select(p => p.StockQuantity).FirstAsync());

    public static object GuestOrderBody(Guid productId, int quantity, string email, string payment = "COD", string? voucherCode = null) => new
    {
        voucherCode,
        email,
        fullName = "Guest Buyer",
        shippingAddress = new
        {
            fullName = "Guest Buyer",
            phoneNumber = "0901234567",
            addressLine = "1 Test Street",
            ward = "W1",
            district = "D1",
            city = "HCM"
        },
        paymentMethod = payment,
        items = new[] { new { productId, quantity } }
    };

    public static async Task<Order> CreateOrderAsync(
        this ApiFactory factory, Guid? userId = null, string? guestEmail = null, decimal total = 100_000m,
        PaymentMethod method = PaymentMethod.BankTransfer, long? payOsCode = null)
    {
        var order = new Order
        {
            OrderNumber = "ORD-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            UserId = userId,
            GuestEmail = guestEmail?.ToLowerInvariant(),
            GuestFullName = guestEmail == null ? null : "Guest",
            TotalAmount = total,
            PaymentMethod = method,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Processing,
            ShippingFullName = "Receiver",
            ShippingPhoneNumber = "0901234567",
            ShippingAddressLine = "1 Test Street",
            PayOSOrderCode = payOsCode
        };
        await factory.WithDb(async db =>
        {
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        });
        return order;
    }

    public static Task<Order> ReloadAsync(this ApiFactory factory, Guid orderId) =>
        factory.WithDb(db => db.Orders.AsNoTracking().Include(o => o.Payments).FirstAsync(o => o.Id == orderId));

    /// <summary>Signs a PayOS webhook exactly like PayOS does (sorted fields, HMAC-SHA256 with the checksum key).</summary>
    public static PayOSWebhookRequest SignedWebhook(long orderCode, int amount, string code = "00", string? checksumKey = null)
    {
        var data = new PayOSWebhookData
        {
            OrderCode = orderCode,
            Amount = amount,
            Description = "DH test",
            AccountNumber = "123456",
            Reference = "REF" + orderCode,
            TransactionDateTime = "2026-01-01 10:00:00",
            Currency = "VND",
            PaymentLinkId = "pl-" + orderCode,
            Code = code,
            Desc = "success"
        };
        return new PayOSWebhookRequest
        {
            Code = code,
            Desc = "success",
            Success = code == "00",
            Data = data,
            Signature = Sign(data, checksumKey ?? ApiFactory.PayOSChecksumKey)
        };
    }

    public static string Sign(PayOSWebhookData d, string key)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["accountNumber"] = d.AccountNumber,
            ["amount"] = d.Amount.ToString(),
            ["code"] = d.Code,
            ["counterAccountBankId"] = d.CounterAccountBankId,
            ["counterAccountBankName"] = d.CounterAccountBankName,
            ["counterAccountName"] = d.CounterAccountName,
            ["counterAccountNumber"] = d.CounterAccountNumber,
            ["currency"] = d.Currency,
            ["desc"] = d.Desc,
            ["description"] = d.Description,
            ["orderCode"] = d.OrderCode.ToString(),
            ["paymentLinkId"] = d.PaymentLinkId,
            ["reference"] = d.Reference,
            ["transactionDateTime"] = d.TransactionDateTime,
            ["virtualAccountName"] = d.VirtualAccountName,
            ["virtualAccountNumber"] = d.VirtualAccountNumber
        };
        var canonical = string.Join("&", fields.Select(kv => $"{kv.Key}={kv.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
