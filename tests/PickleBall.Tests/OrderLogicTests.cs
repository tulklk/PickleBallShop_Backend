using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

/// <summary>Business-logic abuse: negative/huge quantities, client-side prices, stock races, guest order lookup.</summary>
public class OrderLogicTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public OrderLogicTests(ApiFactory factory) => _factory = factory;

    private HttpClient Client => _factory.CreateClient();

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public async Task Guest_order_rejects_invalid_quantities_and_does_not_touch_stock(int quantity)
    {
        var product = await _factory.CreateProductAsync(stock: 10);
        var response = await Client.PostAsJsonAsync("/api/guest/orders",
            TestHelpers.GuestOrderBody(product.Id, quantity, TestHelpers.UniqueEmail("guest")), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(10, await _factory.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Guest_order_rejects_empty_cart_and_unknown_payment_method()
    {
        var product = await _factory.CreateProductAsync();
        var email = TestHelpers.UniqueEmail("guest");

        var empty = await Client.PostAsJsonAsync("/api/guest/orders", new
        {
            email, fullName = "G",
            shippingAddress = new { fullName = "G", phoneNumber = "0901234567", addressLine = "x" },
            paymentMethod = "COD", items = Array.Empty<object>()
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var body = TestHelpers.GuestOrderBody(product.Id, 1, email, payment: "COD");
        var json = JsonSerializer.Serialize(body, TestHelpers.Json).Replace("\"COD\"", "99");
        var bad = await Client.PostAsync("/api/guest/orders",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Guest_order_total_is_computed_by_the_server_and_ignores_client_prices()
    {
        var product = await _factory.CreateProductAsync(price: 250_000m, stock: 10);
        var email = TestHelpers.UniqueEmail("guest");

        var json = JsonSerializer.Serialize(TestHelpers.GuestOrderBody(product.Id, 2, email), TestHelpers.Json);
        // attacker adds price / total fields (mass assignment attempt)
        json = json.Replace("\"quantity\":2", "\"quantity\":2,\"unitPrice\":1,\"price\":1")
                   .TrimEnd('}') + ",\"totalAmount\":1,\"discount\":999999}";

        var response = await Client.PostAsync("/api/guest/orders",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var order = doc.RootElement.GetProperty("order");
        Assert.Equal(500_000m, order.GetProperty("totalAmount").GetDecimal());
        Assert.Matches("^ORD-\\d{8}-[0-9A-Fa-f]{12}$", order.GetProperty("orderNumber").GetString()!);
        Assert.Equal(8, await _factory.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Order_numbers_are_not_sequential_or_guessable()
    {
        var product = await _factory.CreateProductAsync(stock: 50);
        var numbers = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var r = await Client.PostAsJsonAsync("/api/guest/orders",
                TestHelpers.GuestOrderBody(product.Id, 1, TestHelpers.UniqueEmail("guest")), TestHelpers.Json);
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            numbers.Add(doc.RootElement.GetProperty("order").GetProperty("orderNumber").GetString()!);
        }
        Assert.Equal(3, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.True(n.Length >= 25));
    }

    [Fact]
    public async Task Cannot_order_more_than_the_stock_and_stock_is_left_unchanged()
    {
        var product = await _factory.CreateProductAsync(stock: 3);
        var response = await Client.PostAsJsonAsync("/api/guest/orders",
            TestHelpers.GuestOrderBody(product.Id, 4, TestHelpers.UniqueEmail("guest")), TestHelpers.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(3, await _factory.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Duplicate_lines_are_merged_and_the_merged_quantity_is_capped()
    {
        var product = await _factory.CreateProductAsync(stock: 1000);
        var body = new
        {
            email = TestHelpers.UniqueEmail("guest"),
            fullName = "G",
            shippingAddress = new { fullName = "G", phoneNumber = "0901234567", addressLine = "x" },
            paymentMethod = "COD",
            items = new[] { new { productId = product.Id, quantity = 60 }, new { productId = product.Id, quantity = 60 } }
        };
        var response = await Client.PostAsJsonAsync("/api/guest/orders", body, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1000, await _factory.StockOfAsync(product.Id));
    }

    [Fact]
    public async Task Inactive_products_cannot_be_ordered()
    {
        var product = await _factory.CreateProductAsync(active: false);
        var response = await Client.PostAsJsonAsync("/api/guest/orders",
            TestHelpers.GuestOrderBody(product.Id, 1, TestHelpers.UniqueEmail("guest")), TestHelpers.Json);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Voucher_preview_for_guests_validates_quantities()
    {
        var product = await _factory.CreateProductAsync();
        var response = await Client.PostAsJsonAsync("/api/guest/orders/apply-voucher", new
        {
            email = TestHelpers.UniqueEmail("g"), code = "ANY",
            items = new[] { new { productId = product.Id, quantity = -5 } }
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(string Email, string OrderNumber, Guid OrderId)> PlaceGuestOrderAsync(string? email = null)
    {
        var product = await _factory.CreateProductAsync(stock: 100);
        email ??= TestHelpers.UniqueEmail("guest");
        var response = await Client.PostAsJsonAsync("/api/guest/orders",
            TestHelpers.GuestOrderBody(product.Id, 1, email), TestHelpers.Json);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var order = doc.RootElement.GetProperty("order");
        return (email, order.GetProperty("orderNumber").GetString()!, order.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Guest_lookup_requires_both_order_number_and_email_and_returns_a_single_order()
    {
        var (email, number, id) = await PlaceGuestOrderAsync();
        await PlaceGuestOrderAsync(email); // second order by the same email must never be listed

        // Email alone (old behaviour: list every order of an address) is refused
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/guest/orders/lookup?email={Uri.EscapeDataString(email)}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/guest/orders/lookup?orderNumber={number}")).StatusCode);

        // Wrong email / wrong number -> same 404
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/guest/orders/lookup?orderNumber={number}&email=other@example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/guest/orders/lookup?orderNumber=ORD-NOPE&email={Uri.EscapeDataString(email)}")).StatusCode);

        var ok = await Client.GetAsync($"/api/guest/orders/lookup?orderNumber={number}&email={Uri.EscapeDataString(email.ToUpperInvariant())}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        using var doc = JsonDocument.Parse(await ok.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal(number, doc.RootElement[0].GetProperty("orderNumber").GetString());
    }

    [Fact]
    public async Task Guest_order_status_and_detail_require_the_matching_email()
    {
        var (email, number, id) = await PlaceGuestOrderAsync();
        var enc = Uri.EscapeDataString(email);

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/guest/orders/status/{number}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/guest/orders/status/{number}?email=other@example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/guest/orders/status/{number}?email={enc}")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync($"/api/guest/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/guest/orders/{id}?email=other@example.test")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/guest/orders/{id}?email={enc}")).StatusCode);

        // A registered user's order is not reachable through the guest endpoints, even with the right email
        var user = await _factory.CreateUserAsync();
        var userOrder = await _factory.CreateOrderAsync(userId: user.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/api/guest/orders/status/{userOrder.OrderNumber}?email={Uri.EscapeDataString(user.Email)}")).StatusCode);
    }

    [Fact]
    public async Task Guest_payment_cannot_be_initiated_for_somebody_elses_order()
    {
        var (email, number, id) = await PlaceGuestOrderAsync();
        var response = await Client.PostAsync($"/api/guest/orders/{id}/payment?email=attacker@example.test", null);
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cart_quantity_is_bounded()
    {
        var user = await _factory.CreateUserAsync();
        var client = await _factory.LoggedInClientAsync(user);
        var product = await _factory.CreateProductAsync(stock: 1000);

        foreach (var qty in new[] { -3, 0, 101 })
        {
            var r = await client.PostAsJsonAsync("/api/Cart/items", new { productId = product.Id, quantity = qty }, TestHelpers.Json);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/Cart/items", new { productId = product.Id, quantity = 2 }, TestHelpers.Json)).StatusCode);
    }
}
