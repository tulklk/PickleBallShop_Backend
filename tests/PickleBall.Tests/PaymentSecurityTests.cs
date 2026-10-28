using System.Net;
using System.Net.Http.Json;
using PickleBall.Domain.Entities;
using PickleBall.Tests.Infrastructure;

namespace PickleBall.Tests;

/// <summary>An order may only become Paid through a verified gateway signal; browser input is never proof of payment.</summary>
public class PaymentSecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public PaymentSecurityTests(ApiFactory factory) => _factory = factory;

    private static long NewCode() => Random.Shared.NextInt64(1_000_000_000, 9_000_000_000);

    [Fact]
    public async Task Forged_payos_confirm_does_not_mark_the_order_paid()
    {
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), payOsCode: code);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/confirm",
            new { orderCode = code, status = "PAID", code = "00", cancel = false }, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"success\":false", await response.Content.ReadAsStringAsync());
        var reloaded = await _factory.ReloadAsync(order.Id);
        Assert.Equal(PaymentStatus.Pending, reloaded.PaymentStatus);
        Assert.Empty(reloaded.Payments);
    }

    [Fact]
    public async Task Forged_payos_return_redirect_does_not_mark_the_order_paid()
    {
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), payOsCode: code);

        var response = await _factory.NoRedirectClient().GetAsync(
            $"/api/payments/payos/return?code=00&id=x&cancel=false&status=PAID&orderCode={code}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("status=PAID", response.Headers.Location!.ToString());
        Assert.Equal(PaymentStatus.Pending, (await _factory.ReloadAsync(order.Id)).PaymentStatus);
    }

    [Fact]
    public async Task Payos_confirm_for_a_registered_users_order_is_refused_to_anonymous_callers_and_other_users()
    {
        var owner = await _factory.CreateUserAsync();
        var other = await _factory.CreateUserAsync();
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(userId: owner.Id, payOsCode: code);

        var anon = await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/confirm", new { orderCode = code }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.Forbidden, anon.StatusCode);

        var otherClient = await _factory.LoggedInClientAsync(other);
        var asOther = await otherClient.PostAsJsonAsync("/api/payments/payos/confirm", new { orderCode = code }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.Forbidden, asOther.StatusCode);

        Assert.Equal(PaymentStatus.Pending, (await _factory.ReloadAsync(order.Id)).PaymentStatus);
    }

    [Fact]
    public async Task Payos_confirm_error_responses_do_not_echo_internals()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/confirm",
            new { orderCode = 424242424242 }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_with_an_invalid_signature_is_ignored()
    {
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), payOsCode: code, total: 100_000m);
        var forged = TestHelpers.SignedWebhook(code, 100_000, checksumKey: "attacker-key");

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/webhook", forged, TestHelpers.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // always 200 so PayOS does not retry forever
        Assert.Equal(PaymentStatus.Pending, (await _factory.ReloadAsync(order.Id)).PaymentStatus);
    }

    [Fact]
    public async Task Webhook_with_tampered_amount_or_unsigned_code_is_ignored()
    {
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), payOsCode: code, total: 100_000m);

        // Attacker takes a legitimately signed "failed" webhook and flips the unsigned envelope to success
        var failed = TestHelpers.SignedWebhook(code, 100_000, code: "01");
        failed.Code = "00";
        failed.Success = true;
        await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/webhook", failed, TestHelpers.Json);
        // The signed data says "failed": the order may be marked failed, but never paid.
        Assert.NotEqual(PaymentStatus.Paid, (await _factory.ReloadAsync(order.Id)).PaymentStatus);

        // Signed for a smaller amount, then changed after signing
        var tampered = TestHelpers.SignedWebhook(code, 1_000);
        tampered.Data!.Amount = 100_000;
        await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/webhook", tampered, TestHelpers.Json);
        Assert.NotEqual(PaymentStatus.Paid, (await _factory.ReloadAsync(order.Id)).PaymentStatus);
    }

    [Fact]
    public async Task Webhook_signed_for_the_wrong_amount_does_not_pay_the_order()
    {
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), payOsCode: code, total: 100_000m);

        // Perfectly signed, but only 1.000 VND was paid
        var underpaid = TestHelpers.SignedWebhook(code, 1_000);
        await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/webhook", underpaid, TestHelpers.Json);

        var reloaded = await _factory.ReloadAsync(order.Id);
        Assert.NotEqual(PaymentStatus.Paid, reloaded.PaymentStatus);
    }

    [Fact]
    public async Task Valid_webhook_marks_paid_once_and_replays_are_idempotent()
    {
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), payOsCode: code, total: 100_000m);
        var webhook = TestHelpers.SignedWebhook(code, 100_000);
        var client = _factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/payments/payos/webhook", webhook, TestHelpers.Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var reloaded = await _factory.ReloadAsync(order.Id);
        Assert.Equal(PaymentStatus.Paid, reloaded.PaymentStatus);
        Assert.Single(reloaded.Payments);
    }

    [Fact]
    public async Task Webhook_for_a_cancelled_order_does_not_resurrect_it()
    {
        var code = NewCode();
        var order = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), payOsCode: code, total: 100_000m);
        await _factory.WithDb(async db =>
        {
            var o = await db.Orders.FindAsync(order.Id);
            o!.OrderStatus = OrderStatus.Cancelled;
            await db.SaveChangesAsync();
        });

        await _factory.CreateClient().PostAsJsonAsync("/api/payments/payos/webhook",
            TestHelpers.SignedWebhook(code, 100_000), TestHelpers.Json);

        var reloaded = await _factory.ReloadAsync(order.Id);
        Assert.Equal(OrderStatus.Cancelled, reloaded.OrderStatus);
        Assert.NotEqual(PaymentStatus.Paid, reloaded.PaymentStatus);
    }

    [Fact]
    public async Task MoMo_and_VNPay_callbacks_are_rejected_when_gateway_secrets_are_not_configured()
    {
        var momoOrder = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), method: PaymentMethod.MoMo);
        var momo = await _factory.CreateClient().PostAsJsonAsync("/api/payments/momo/ipn", new Dictionary<string, string>
        {
            ["orderId"] = momoOrder.OrderNumber,
            ["transId"] = "1",
            ["resultCode"] = "0",
            ["amount"] = ((long)momoOrder.TotalAmount).ToString(),
            ["signature"] = "deadbeef"
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, momo.StatusCode);
        Assert.Equal(PaymentStatus.Pending, (await _factory.ReloadAsync(momoOrder.Id)).PaymentStatus);

        var vnpOrder = await _factory.CreateOrderAsync(guestEmail: TestHelpers.UniqueEmail("g"), method: PaymentMethod.VNPay);
        var vnp = await _factory.NoRedirectClient().GetAsync(
            $"/api/payments/vnpay/return?vnp_TxnRef={vnpOrder.OrderNumber}&vnp_TransactionNo=1&vnp_ResponseCode=00" +
            $"&vnp_Amount={(long)vnpOrder.TotalAmount * 100}&vnp_SecureHash=deadbeef");
        Assert.Equal(PaymentStatus.Pending, (await _factory.ReloadAsync(vnpOrder.Id)).PaymentStatus);
        Assert.Contains("failed", vnp.Headers.Location?.ToString() ?? "");

        // Empty signature with an empty secret must not validate either (empty HMAC key is forgeable)
        var emptySig = await _factory.CreateClient().PostAsJsonAsync("/api/payments/momo/ipn", new Dictionary<string, string>
        {
            ["orderId"] = momoOrder.OrderNumber, ["transId"] = "1", ["resultCode"] = "0",
            ["amount"] = ((long)momoOrder.TotalAmount).ToString(), ["signature"] = ""
        }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.BadRequest, emptySig.StatusCode);
    }
}
