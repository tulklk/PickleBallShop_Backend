using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMoMoPaymentService _moMoService;
    private readonly IVnPayPaymentService _vnPayService;
    private readonly IPayOSService _payOSService;
    private readonly IOrderService _orderService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ApplicationDbContext context,
        IMoMoPaymentService moMoService,
        IVnPayPaymentService vnPayService,
        IPayOSService payOSService,
        IOrderService orderService,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _moMoService = moMoService;
        _vnPayService = vnPayService;
        _payOSService = payOSService;
        _orderService = orderService;
        _logger = logger;
    }

    [HttpGet("momo/return")]
    public async Task<IActionResult> MoMoReturn([FromQuery] Dictionary<string, string> callbackData)
    {
        try
        {
            var isValid = await _moMoService.VerifyCallbackAsync(callbackData);

            if (!isValid)
            {
                _logger.LogWarning("Invalid MoMo callback signature");
                return Redirect("/payment/failed");
            }

            if (callbackData.TryGetValue("orderId", out var orderNumber) &&
                callbackData.TryGetValue("transId", out var transactionId) &&
                callbackData.TryGetValue("resultCode", out var resultCode))
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order != null)
                {
                    var isPaid = resultCode == "0"; // MoMo success code
                    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);

                    if (isPaid)
                    {
                        // Save payment record
                        var payment = new Payment
                        {
                            OrderId = order.Id,
                            UserId = order.UserId,
                            PaymentGateway = PaymentMethod.MoMo,
                            Amount = order.TotalAmount,
                            Currency = "VND",
                            TransactionId = transactionId,
                            Status = PaymentStatus.Paid,
                            RawResponse = System.Text.Json.JsonSerializer.Serialize(callbackData)
                        };
                        _context.Payments.Add(payment);
                        await _context.SaveChangesAsync();

                        return Redirect("/payment/success");
                    }
                }
            }

            return Redirect("/payment/failed");
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
            var isValid = await _moMoService.VerifyCallbackAsync(callbackData);

            if (!isValid)
            {
                _logger.LogWarning("Invalid MoMo IPN signature");
                return BadRequest();
            }

            if (callbackData.TryGetValue("orderId", out var orderNumber) &&
                callbackData.TryGetValue("transId", out var transactionId) &&
                callbackData.TryGetValue("resultCode", out var resultCode))
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order != null)
                {
                    var isPaid = resultCode == "0";
                    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);

                    if (isPaid)
                    {
                        // Check if payment record already exists
                        var existingPayment = await _context.Payments
                            .FirstOrDefaultAsync(p => p.OrderId == order.Id && p.TransactionId == transactionId);

                        if (existingPayment == null)
                        {
                            var payment = new Payment
                            {
                                OrderId = order.Id,
                                UserId = order.UserId,
                                PaymentGateway = PaymentMethod.MoMo,
                                Amount = order.TotalAmount,
                                Currency = "VND",
                                TransactionId = transactionId,
                                Status = PaymentStatus.Paid,
                                RawResponse = System.Text.Json.JsonSerializer.Serialize(callbackData)
                            };
                            _context.Payments.Add(payment);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
            return StatusCode(500);
        }
    }

    [HttpGet("vnpay/return")]
    public async Task<IActionResult> VnPayReturn([FromQuery] Dictionary<string, string> callbackData)
    {
        try
        {
            var isValid = await _vnPayService.VerifyCallbackAsync(callbackData);

            if (!isValid)
            {
                _logger.LogWarning("Invalid VNPay callback signature");
                return Redirect("/payment/failed");
            }

            if (callbackData.TryGetValue("vnp_TxnRef", out var orderNumber) &&
                callbackData.TryGetValue("vnp_TransactionNo", out var transactionId) &&
                callbackData.TryGetValue("vnp_ResponseCode", out var responseCode))
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order != null)
                {
                    var isPaid = responseCode == "00"; // VNPay success code
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid);

                    if (isPaid)
                    {
                        var payment = new Payment
                        {
                            OrderId = order.Id,
                            UserId = order.UserId,
                            PaymentGateway = PaymentMethod.VNPay,
                            Amount = order.TotalAmount,
                            Currency = "VND",
                            TransactionId = transactionId,
                            Status = PaymentStatus.Paid,
                            RawResponse = System.Text.Json.JsonSerializer.Serialize(callbackData)
                        };
                        _context.Payments.Add(payment);
                        await _context.SaveChangesAsync();

                        return Redirect("/payment/success");
                    }
                }
            }

            return Redirect("/payment/failed");
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
            var isValid = await _vnPayService.VerifyCallbackAsync(callbackData);

            if (!isValid)
            {
                _logger.LogWarning("Invalid VNPay IPN signature");
                return BadRequest();
            }

            if (callbackData.TryGetValue("vnp_TxnRef", out var orderNumber) &&
                callbackData.TryGetValue("vnp_TransactionNo", out var transactionId) &&
                callbackData.TryGetValue("vnp_ResponseCode", out var responseCode))
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order != null)
                {
                    var isPaid = responseCode == "00";
                    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);

                    if (isPaid)
                    {
                        var existingPayment = await _context.Payments
                            .FirstOrDefaultAsync(p => p.OrderId == order.Id && p.TransactionId == transactionId);

                        if (existingPayment == null)
                        {
                            var payment = new Payment
                            {
                                OrderId = order.Id,
                                UserId = order.UserId,
                                PaymentGateway = PaymentMethod.VNPay,
                                Amount = order.TotalAmount,
                                Currency = "VND",
                                TransactionId = transactionId,
                                Status = PaymentStatus.Paid,
                                RawResponse = System.Text.Json.JsonSerializer.Serialize(callbackData)
                            };
                            _context.Payments.Add(payment);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VNPay IPN");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// PayOS webhook endpoint to receive payment notifications
    /// </summary>
    [HttpPost("payos/webhook")]
    public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookRequest? webhookData)
    {
        try
        {
            // Handle PayOS test/ping request
            if (webhookData == null || string.IsNullOrEmpty(webhookData.Code))
            {
                _logger.LogInformation("Received PayOS test/ping webhook");
                return Ok(new { success = true, message = "Webhook received" });
            }

            _logger.LogInformation("Received PayOS webhook: Code={Code}, Success={Success}", 
                webhookData.Code, webhookData.Success);

            var verifiedData = await _payOSService.VerifyWebhookAsync(webhookData);

            if (verifiedData == null)
            {
                _logger.LogWarning("Invalid PayOS webhook signature");
                // Return OK for test webhooks to pass PayOS validation
                return Ok(new { success = true, message = "Webhook processed" });
            }

            // Find order by order code
            var orderCode = verifiedData.OrderCode;
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNumber.Contains(orderCode.ToString().Substring(0, 8)));

            if (order == null)
            {
                // Try to find by matching description
                _logger.LogWarning("Order not found for PayOS orderCode: {OrderCode}", orderCode);
                return Ok(new { message = "Order not found" });
            }

            var isPaid = webhookData.Code == "00" && webhookData.Success;
            var transactionId = verifiedData.Reference;
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);

            if (isPaid)
            {
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.OrderId == order.Id && p.TransactionId == transactionId);

                if (existingPayment == null)
                {
                    var payment = new Payment
                    {
                        OrderId = order.Id,
                        UserId = order.UserId,
                        PaymentGateway = PaymentMethod.BankTransfer,
                        Amount = verifiedData.Amount,
                        Currency = verifiedData.Currency,
                        TransactionId = transactionId,
                        Status = PaymentStatus.Paid,
                        RawResponse = System.Text.Json.JsonSerializer.Serialize(webhookData)
                    };
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("PayOS payment successful for Order {OrderId}, TransactionId: {TransactionId}", 
                    order.Id, transactionId);
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
    /// PayOS callback confirmation from frontend - updates order payment status
    /// </summary>
    [HttpPost("payos/confirm")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> PayOSConfirm([FromBody] PayOSConfirmRequest request)
    {
        try
        {
            _logger.LogInformation("PayOS confirm: code={Code}, status={Status}, orderCode={OrderCode}, cancel={Cancel}, orderId={OrderId}", 
                request.Code, request.Status, request.OrderCode, request.Cancel, request.OrderId);

            if (request.Cancel || request.Status == "CANCELLED")
            {
                return Ok(new { success = false, message = "Payment was cancelled" });
            }

            if (request.Code == "00" && request.Status == "PAID")
            {
                // Find order by ID from request - load with all navigation properties needed for email
                Order? order = null;
                
                if (request.OrderId.HasValue)
                {
                    order = await _context.Orders
                        .Include(o => o.User)
                        .Include(o => o.Items)
                            .ThenInclude(oi => oi.Product)
                                .ThenInclude(p => p.Images)
                        .FirstOrDefaultAsync(o => o.Id == request.OrderId.Value);
                }
                
                // If not found by ID, try to find the most recent pending BankTransfer order for the user
                if (order == null)
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
                    {
                        order = await _context.Orders
                            .Include(o => o.User)
                            .Include(o => o.Items)
                                .ThenInclude(oi => oi.Product)
                                    .ThenInclude(p => p.Images)
                            .Where(o => o.UserId == userGuid && 
                                       o.PaymentMethod == PaymentMethod.BankTransfer && 
                                       o.PaymentStatus == PaymentStatus.Pending)
                            .OrderByDescending(o => o.CreatedAt)
                            .FirstOrDefaultAsync();
                    }
                }

                if (order == null)
                {
                    _logger.LogWarning("Order not found for PayOS confirm");
                    return NotFound(new { success = false, message = "Order not found" });
                }

                // Update order payment status - this will also send email
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                try
                {
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, request.OrderCode.ToString(), true, clientIp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating order payment status for Order {OrderId}", order.Id);
                    // Continue to create payment record even if email fails
                }

                // Create payment record if not exists
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.OrderId == order.Id);

                if (existingPayment == null)
                {
                    var payment = new Payment
                    {
                        OrderId = order.Id,
                        UserId = order.UserId,
                        PaymentGateway = PaymentMethod.BankTransfer,
                        Amount = order.TotalAmount,
                        Currency = "VND",
                        TransactionId = request.OrderCode.ToString(),
                        Status = PaymentStatus.Paid,
                        RawResponse = System.Text.Json.JsonSerializer.Serialize(request)
                    };
                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("PayOS payment confirmed for Order {OrderId}", order.Id);
                return Ok(new { success = true, message = "Payment confirmed", orderId = order.Id });
            }

            return Ok(new { success = false, message = "Payment not successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS confirm");
            return StatusCode(500, new { success = false, message = "Error processing payment confirmation" });
        }
    }
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
