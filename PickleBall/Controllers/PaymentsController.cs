using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using PickleBall.Infrastructure.Services;

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
                    
                    // Update order payment status - this will also send email
                    try
                    {
                        await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating order payment status for Order {OrderId}", order.Id);
                        // Continue to save payment record even if update fails
                    }

                    if (isPaid)
                    {
                        // Save payment record
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
                    
                    // Update order payment status - this will also send email
                    try
                    {
                        await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating order payment status for Order {OrderId}", order.Id);
                        // Continue to save payment record even if update fails
                    }

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
                    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                    
                    // Update order payment status - this will also send email
                    try
                    {
                        await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating order payment status for Order {OrderId}", order.Id);
                        // Continue to save payment record even if update fails
                    }

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
                    
                    // Update order payment status - this will also send email
                    try
                    {
                        await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating order payment status for Order {OrderId}", order.Id);
                        // Continue to save payment record even if update fails
                    }

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
    /// PayOS return URL handler - redirects user after payment
    /// </summary>
    [HttpGet("payos/return")]
    public async Task<IActionResult> PayOSReturn([FromQuery] PayOSReturnRequest returnData)
    {
        try
        {
            _logger.LogInformation("PayOS return: code={Code}, status={Status}, orderCode={OrderCode}, cancel={Cancel}", 
                returnData.Code, returnData.Status, returnData.OrderCode, returnData.Cancel);

            // Check if payment was cancelled
            if (returnData.Cancel || returnData.Status == "CANCELLED")
            {
                _logger.LogInformation("PayOS payment cancelled for orderCode: {OrderCode}", returnData.OrderCode);
                return Redirect($"{GetFrontendUrl()}/payments/payos/return?code={returnData.Code}&status={returnData.Status}&orderCode={returnData.OrderCode}&cancel=true&paymentStatus=cancelled");
            }

            // Check if payment was successful
            if (returnData.Code == "00" && returnData.Status == "PAID")
            {
                // Try to find order by orderCode from cache first
                Guid? orderId = PayOSService.GetOrderIdFromOrderCode(returnData.OrderCode);
                
                Order? order = null;
                
                // If found in cache, get order by ID
                if (orderId.HasValue)
                {
                    order = await _context.Orders
                        .Include(o => o.User)
                        .Include(o => o.Items)
                            .ThenInclude(oi => oi.Product)
                                .ThenInclude(p => p.Images)
                        .FirstOrDefaultAsync(o => o.Id == orderId.Value);
                }
                
                // If not found in cache or by ID, try to find by orderCode in OrderNumber
                if (order == null)
                {
                    var orderCodeStr = returnData.OrderCode.ToString();
                    _logger.LogInformation("OrderId not found in cache, searching database for orderCode: {OrderCode}", returnData.OrderCode);
                    
                    order = await _context.Orders
                        .Include(o => o.User)
                        .Include(o => o.Items)
                            .ThenInclude(oi => oi.Product)
                                .ThenInclude(p => p.Images)
                        .FirstOrDefaultAsync(o => o.OrderNumber.Contains(orderCodeStr));
                }

                if (order != null)
                {
                    var transactionId = returnData.Id ?? returnData.OrderCode.ToString();
                    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                    // Check if order is already paid
                    if (order.PaymentStatus != PaymentStatus.Paid)
                    {
                        // Update order payment status - this will also send email
                        try
                        {
                            await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, true, clientIp);
                            _logger.LogInformation("PayOS payment successful for Order {OrderId}, TransactionId: {TransactionId}", 
                                order.Id, transactionId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating order payment status for Order {OrderId}", order.Id);
                            // Continue to save payment record even if update fails
                        }

                        // Create payment record if not exists
                        var existingPayment = await _context.Payments
                            .FirstOrDefaultAsync(p => p.OrderId == order.Id && p.TransactionId == transactionId);

                        if (existingPayment == null)
                        {
                            var payment = new Payment
                            {
                                OrderId = order.Id,
                                UserId = order.UserId,
                                PaymentGateway = PaymentMethod.BankTransfer,
                                Amount = order.TotalAmount,
                                Currency = "VND",
                                TransactionId = transactionId,
                                Status = PaymentStatus.Paid,
                                RawResponse = System.Text.Json.JsonSerializer.Serialize(returnData)
                            };
                            _context.Payments.Add(payment);
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Order {OrderId} is already paid. Redirecting to success page.", order.Id);
                    }

                    // Redirect to success page with order info (regardless of whether already paid or not)
                    // Send orderId in multiple param names to ensure frontend can get it
                    return Redirect($"{GetFrontendUrl()}/payments/payos/return?code=00&status=PAID&orderCode={returnData.OrderCode}&orderId={order.Id}&savedOrderId={order.Id}");
                }
                else
                {
                    _logger.LogWarning("Order not found in database for PayOS orderCode: {OrderCode}", returnData.OrderCode);
                }
            }

            // Payment failed or order not found
            _logger.LogWarning("PayOS payment failed or order not found: code={Code}, status={Status}, orderCode={OrderCode}", 
                returnData.Code, returnData.Status, returnData.OrderCode);
            return Redirect($"{GetFrontendUrl()}/payments/payos/return?code={returnData.Code}&status={returnData.Status}&orderCode={returnData.OrderCode}&paymentStatus=failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS return");
            return Redirect($"{GetFrontendUrl()}/payments/payos/return?code=99&status=ERROR&orderCode={returnData.OrderCode}&paymentStatus=error&message={Uri.EscapeDataString(ex.Message)}");
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

            // Find order by order code using cache
            var orderCode = verifiedData.OrderCode;
            Guid? orderId = PayOSService.GetOrderIdFromOrderCode(orderCode);
            
            if (orderId == null)
            {
                _logger.LogWarning("OrderId not found in cache for PayOS orderCode: {OrderCode}", orderCode);
                return Ok(new { message = "Order not found in cache" });
            }
            
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId.Value);

            if (order == null)
            {
                _logger.LogWarning("Order not found in database for OrderId: {OrderId}", orderId.Value);
                return Ok(new { message = "Order not found" });
            }

            var isPaid = webhookData.Code == "00" && webhookData.Success;
            var transactionId = verifiedData.Reference;
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Update order payment status - this will also send email
            try
            {
                await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid, clientIp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order payment status for Order {OrderId}", order.Id);
                // Continue to save payment record even if update fails
            }

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
                
                // If not found by ID, try to find by orderCode
                if (order == null && request.OrderCode > 0)
                {
                    var orderCodeStr = request.OrderCode.ToString();
                    order = await _context.Orders
                        .Include(o => o.User)
                        .Include(o => o.Items)
                            .ThenInclude(oi => oi.Product)
                                .ThenInclude(p => p.Images)
                        .FirstOrDefaultAsync(o => o.OrderNumber.Contains(orderCodeStr));
                }
                
                // If still not found, try to find the most recent BankTransfer order for the user
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
                                       o.PaymentMethod == PaymentMethod.BankTransfer)
                            .OrderByDescending(o => o.CreatedAt)
                            .FirstOrDefaultAsync();
                    }
                }

                if (order == null)
                {
                    _logger.LogWarning("Order not found for PayOS confirm. OrderCode: {OrderCode}, OrderId: {OrderId}", 
                        request.OrderCode, request.OrderId);
                    return NotFound(new { success = false, message = "Order not found", orderCode = request.OrderCode });
                }

                // Check if order is already paid - if so, just return success
                if (order.PaymentStatus == PaymentStatus.Paid)
                {
                    _logger.LogInformation("Order {OrderId} is already paid. Returning success.", order.Id);
                    return Ok(new { success = true, message = "Payment already confirmed", orderId = order.Id });
                }

                // Validate order has user with email before updating
                if (order.User == null || string.IsNullOrWhiteSpace(order.User.Email))
                {
                    _logger.LogWarning("Order {OrderId} has no user or user email. Reloading user from database...", order.Id);
                    
                    // Try to reload user from database
                    var userId = order.UserId;
                    var user = await _context.Users.FindAsync(userId);
                    
                    if (user == null || string.IsNullOrWhiteSpace(user.Email))
                    {
                        // Try to get email from JWT token as fallback
                        var emailFromToken = User.FindFirstValue(ClaimTypes.Email);
                        
                        if (string.IsNullOrWhiteSpace(emailFromToken))
                        {
                            _logger.LogError("Cannot send order confirmation email: Order {OrderId} has no user email and cannot get from token", order.Id);
                            return StatusCode(500, new { success = false, message = "Cannot send order confirmation: User email not found" });
                        }
                        
                        // Update user email if we have it from token
                        if (user != null && string.IsNullOrWhiteSpace(user.Email))
                        {
                            user.Email = emailFromToken;
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Updated user {UserId} email from JWT token", userId);
                        }
                        
                        // Reload order with updated user
                        order = await _context.Orders
                            .Include(o => o.User)
                            .Include(o => o.Items)
                                .ThenInclude(oi => oi.Product)
                                    .ThenInclude(p => p.Images)
                            .FirstOrDefaultAsync(o => o.Id == order.Id);
                    }
                }

                // Update order payment status - this will also send email
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                try
                {
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, request.OrderCode.ToString(), true, clientIp);
                    _logger.LogInformation("Order payment status updated and email sent for Order {OrderId}", order.Id);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("email"))
                {
                    _logger.LogError(ex, "Failed to send order confirmation email for Order {OrderId}: {Error}", order.Id, ex.Message);
                    // Update payment status without email
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.OrderStatus = OrderStatus.Confirmed;
                    order.PaymentTransactionId = request.OrderCode.ToString();
                    await _context.SaveChangesAsync();
                    _logger.LogWarning("Order {OrderId} payment status updated without email confirmation", order.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating order payment status for Order {OrderId}: {Error}", order.Id, ex.Message);
                    // Continue to create payment record even if update fails
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

            return Ok(new { success = false, message = "Payment not successful", code = request.Code, status = request.Status, orderCode = request.OrderCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS confirm. OrderCode: {OrderCode}, Error: {Error}", request?.OrderCode, ex.Message);
            return StatusCode(500, new { success = false, message = "Error processing payment confirmation", error = ex.Message, orderCode = request?.OrderCode });
        }
    }

    private string GetFrontendUrl()
    {
        // Get from configuration or use default
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") 
            ?? "https://pickleball-shop-frontend.vercel.app";
        return frontendUrl.TrimEnd('/');
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
