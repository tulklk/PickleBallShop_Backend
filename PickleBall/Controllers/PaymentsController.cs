using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers;

/// <summary>
/// Payment gateway integration.
///
/// SECURITY MODEL: an order only becomes Paid after the payment was verified SERVER-SIDE, by either
///  - a gateway signature (MoMo / VNPay callbacks, PayOS webhook), or
///  - a server-to-server lookup at the gateway (PayOS return / confirm).
/// Browser-supplied query strings or JSON (code=00, status=PAID, ...) are NEVER trusted as proof of payment.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMoMoPaymentService _moMoService;
    private readonly IVnPayPaymentService _vnPayService;
    private readonly IPayOSService _payOSService;
    private readonly IOrderService _orderService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ApplicationDbContext context,
        IMoMoPaymentService moMoService,
        IVnPayPaymentService vnPayService,
        IPayOSService payOSService,
        IOrderService orderService,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _moMoService = moMoService;
        _vnPayService = vnPayService;
        _payOSService = payOSService;
        _orderService = orderService;
        _configuration = configuration;
        _logger = logger;
    }

    // ----------------------------------------------------------------------------------------------
    // MoMo
    // ----------------------------------------------------------------------------------------------

    [HttpGet("momo/return")]
    public async Task<IActionResult> MoMoReturn([FromQuery] Dictionary<string, string> callbackData)
    {
        try
        {
            if (!await _moMoService.VerifyCallbackAsync(callbackData))
            {
                _logger.LogWarning("Invalid MoMo callback signature");
                return Redirect("/payment/failed");
            }

            var paid = await ProcessMoMoAsync(callbackData);
            return Redirect(paid ? "/payment/success" : "/payment/failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo return");
            return Redirect("/payment/failed");
        }
    }

    [HttpPost("momo/ipn")]
    public async Task<IActionResult> MoMoIpn([FromBody] Dictionary<string, string> callbackData)
    {
        try
        {
            if (!await _moMoService.VerifyCallbackAsync(callbackData))
            {
                _logger.LogWarning("Invalid MoMo IPN signature");
                return BadRequest();
            }

            await ProcessMoMoAsync(callbackData);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
            return StatusCode(500);
        }
    }

    private async Task<bool> ProcessMoMoAsync(Dictionary<string, string> data)
    {
        if (!data.TryGetValue("orderId", out var orderNumber) ||
            !data.TryGetValue("transId", out var transactionId) ||
            !data.TryGetValue("resultCode", out var resultCode) ||
            !data.TryGetValue("amount", out var amountText) ||
            !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        return await ApplyGatewayResultAsync(
            PaymentMethod.MoMo, orderNumber, transactionId, resultCode == "0", amount, "VND", data);
    }

    // ----------------------------------------------------------------------------------------------
    // VNPay
    // ----------------------------------------------------------------------------------------------

    [HttpGet("vnpay/return")]
    public async Task<IActionResult> VnPayReturn([FromQuery] Dictionary<string, string> callbackData)
    {
        try
        {
            if (!await _vnPayService.VerifyCallbackAsync(callbackData))
            {
                _logger.LogWarning("Invalid VNPay callback signature");
                return Redirect("/payment/failed");
            }

            var paid = await ProcessVnPayAsync(callbackData);
            return Redirect(paid ? "/payment/success" : "/payment/failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay return");
            return Redirect("/payment/failed");
        }
    }

    [HttpPost("vnpay/ipn")]
    public async Task<IActionResult> VnPayIpn([FromForm] Dictionary<string, string> callbackData)
    {
        try
        {
            if (!await _vnPayService.VerifyCallbackAsync(callbackData))
            {
                _logger.LogWarning("Invalid VNPay IPN signature");
                return BadRequest();
            }

            await ProcessVnPayAsync(callbackData);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay IPN");
            return StatusCode(500);
        }
    }

    private async Task<bool> ProcessVnPayAsync(Dictionary<string, string> data)
    {
        if (!data.TryGetValue("vnp_TxnRef", out var orderNumber) ||
            !data.TryGetValue("vnp_TransactionNo", out var transactionId) ||
            !data.TryGetValue("vnp_ResponseCode", out var responseCode) ||
            !data.TryGetValue("vnp_Amount", out var amountText) ||
            !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var rawAmount))
        {
            return false;
        }

        // VNPay sends the amount multiplied by 100.
        return await ApplyGatewayResultAsync(
            PaymentMethod.VNPay, orderNumber, transactionId, responseCode == "00", rawAmount / 100m, "VND", data);
    }

    // ----------------------------------------------------------------------------------------------
    // PayOS
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Browser landing after PayOS. Query parameters are only hints: payment is verified by asking PayOS directly.
    /// </summary>
    [HttpGet("payos/return")]
    public async Task<IActionResult> PayOSReturn([FromQuery] PayOSReturnRequest returnData)
    {
        var frontend = GetFrontendUrl();

        try
        {
            if (returnData.Cancel || string.Equals(returnData.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect($"{frontend}/payments/payos/return?status=CANCELLED&cancel=true&paymentStatus=cancelled");
            }

            var verified = await VerifyAndApplyPayOSAsync(returnData.OrderCode, expectedUserId: null);
            if (verified.Paid && verified.Order != null)
            {
                return Redirect(
                    $"{frontend}/payments/payos/return?code=00&status=PAID&orderCode={returnData.OrderCode}" +
                    $"&orderId={verified.Order.Id}&savedOrderId={verified.Order.Id}");
            }

            return Redirect($"{frontend}/payments/payos/return?status=FAILED&orderCode={returnData.OrderCode}&paymentStatus=failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS return");
            return Redirect($"{frontend}/payments/payos/return?code=99&status=ERROR&paymentStatus=error");
        }
    }

    /// <summary>
    /// PayOS webhook. The HMAC signature is verified and ONLY signed fields are used.
    /// </summary>
    [HttpPost("payos/webhook")]
    public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookRequest? webhookData)
    {
        try
        {
            // PayOS test/ping request when saving the webhook URL.
            if (webhookData == null || webhookData.Data == null)
            {
                return Ok(new { success = true, message = "Webhook received" });
            }

            var verifiedData = await _payOSService.VerifyWebhookAsync(webhookData);
            if (verifiedData == null)
            {
                _logger.LogWarning("Invalid PayOS webhook signature");
                return Ok(new { success = true, message = "Webhook processed" });
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.PayOSOrderCode == verifiedData.OrderCode);
            if (order == null)
            {
                _logger.LogWarning("PayOS webhook for unknown orderCode {OrderCode}", verifiedData.OrderCode);
                return Ok(new { message = "Order not found" });
            }

            // Use the SIGNED code, not the unsigned outer envelope.
            var isPaid = verifiedData.Code == "00";
            var transactionId = string.IsNullOrWhiteSpace(verifiedData.Reference)
                ? verifiedData.OrderCode.ToString()
                : verifiedData.Reference;
            var currency = string.IsNullOrWhiteSpace(verifiedData.Currency) ? "VND" : verifiedData.Currency;

            if (isPaid)
            {
                await _orderService.ConfirmPaymentAsync(
                    order.Id,
                    PaymentMethod.BankTransfer,
                    transactionId,
                    verifiedData.Amount,
                    currency,
                    JsonSerializer.Serialize(verifiedData),
                    HttpContext.Connection.RemoteIpAddress?.ToString());
            }
            else
            {
                await _orderService.MarkPaymentFailedAsync(order.Id, transactionId);
            }

            return Ok(new { message = "Webhook processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS webhook");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Called by the frontend after PayOS redirects back. The request body is treated as a hint only
    /// (which order code to look at); payment state comes from PayOS, ownership is checked here.
    /// </summary>
    [EnableRateLimiting("payment-confirm")]
    [HttpPost("payos/confirm")]
    public async Task<IActionResult> PayOSConfirm([FromBody] PayOSConfirmRequest request)
    {
        try
        {
            if (request.Cancel || string.Equals(request.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new { success = false, message = "Payment was cancelled" });
            }

            long orderCode = request.OrderCode;
            if (orderCode <= 0 && request.OrderId.HasValue)
            {
                orderCode = await _context.Orders
                    .Where(o => o.Id == request.OrderId.Value && o.PayOSOrderCode != null)
                    .Select(o => o.PayOSOrderCode!.Value)
                    .FirstOrDefaultAsync();
            }

            if (orderCode <= 0)
            {
                return NotFound(new { success = false, message = "Order not found" });
            }

            Guid? currentUserId = null;
            if (User.Identity?.IsAuthenticated == true &&
                Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            {
                currentUserId = uid;
            }

            var result = await VerifyAndApplyPayOSAsync(orderCode, currentUserId, enforceOwnership: true);
            if (result.Forbidden)
            {
                return Forbid();
            }

            if (result.Order == null)
            {
                return NotFound(new { success = false, message = "Order not found" });
            }

            if (result.Paid)
            {
                return Ok(new { success = true, message = "Payment confirmed", orderId = result.Order.Id });
            }

            return Ok(new { success = false, message = "Payment not successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS confirm. OrderCode: {OrderCode}", request?.OrderCode);
            return StatusCode(500, new { success = false, message = "Error processing payment confirmation" });
        }
    }

    private sealed record PayOSVerification(Order? Order, bool Paid, bool Forbidden);

    /// <summary>
    /// Looks the order up by its server-generated PayOS code, asks PayOS for the real payment state,
    /// and marks the order paid only if PayOS says PAID for exactly the order total.
    /// </summary>
    private async Task<PayOSVerification> VerifyAndApplyPayOSAsync(
        long orderCode, Guid? expectedUserId, bool enforceOwnership = false)
    {
        if (orderCode <= 0)
        {
            return new PayOSVerification(null, false, false);
        }

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.PayOSOrderCode == orderCode);
        if (order == null)
        {
            return new PayOSVerification(null, false, false);
        }

        if (enforceOwnership)
        {
            // Registered-user orders: only the owner. Guest orders carry no account, so the unguessable
            // server-generated order code is the capability, and the effect is idempotent and verified at PayOS.
            if (order.UserId != null && order.UserId != expectedUserId)
            {
                return new PayOSVerification(null, false, true);
            }
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return new PayOSVerification(order, true, false);
        }

        var info = await _payOSService.GetPaymentInfoAsync(orderCode);
        if (info == null || !info.IsPaid)
        {
            return new PayOSVerification(order, false, false);
        }

        await _orderService.ConfirmPaymentAsync(
            order.Id,
            PaymentMethod.BankTransfer,
            info.PaymentLinkId ?? orderCode.ToString(),
            info.AmountPaid,
            "VND",
            JsonSerializer.Serialize(info),
            HttpContext.Connection.RemoteIpAddress?.ToString());

        var fresh = await _context.Orders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        return new PayOSVerification(fresh, fresh.PaymentStatus == PaymentStatus.Paid, false);
    }

    // ----------------------------------------------------------------------------------------------
    // Shared
    // ----------------------------------------------------------------------------------------------

    private async Task<bool> ApplyGatewayResultAsync(
        PaymentMethod gateway,
        string orderNumber,
        string transactionId,
        bool isPaid,
        decimal amount,
        string currency,
        Dictionary<string, string> raw)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        if (order == null)
        {
            _logger.LogWarning("{Gateway} callback for unknown order number", gateway);
            return false;
        }

        // The gateway must be the one the order was created for.
        if (order.PaymentMethod != gateway)
        {
            _logger.LogWarning("{Gateway} callback does not match payment method of order {OrderId}", gateway, order.Id);
            return false;
        }

        if (!isPaid)
        {
            await _orderService.MarkPaymentFailedAsync(order.Id, transactionId);
            return false;
        }

        await _orderService.ConfirmPaymentAsync(
            order.Id,
            gateway,
            transactionId,
            amount,
            currency,
            JsonSerializer.Serialize(raw),
            HttpContext.Connection.RemoteIpAddress?.ToString());

        var status = await _context.Orders.AsNoTracking()
            .Where(o => o.Id == order.Id)
            .Select(o => o.PaymentStatus)
            .FirstAsync();
        return status == PaymentStatus.Paid;
    }

    private string GetFrontendUrl()
    {
        var configured = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? _configuration["Email:FrontendUrl"];
        if (string.IsNullOrWhiteSpace(configured) ||
            !Uri.TryCreate(configured, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            configured = "https://pickleball-shop-frontend.vercel.app";
        }

        return configured.TrimEnd('/');
    }
}

public class PayOSReturnRequest
{
    public string Code { get; set; } = string.Empty;
    public string? Id { get; set; }
    public bool Cancel { get; set; }
    public string Status { get; set; } = string.Empty;
    public long OrderCode { get; set; }
}

public class PayOSConfirmRequest
{
    public string Code { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public bool Cancel { get; set; }
    public string Status { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public Guid? OrderId { get; set; }
}
