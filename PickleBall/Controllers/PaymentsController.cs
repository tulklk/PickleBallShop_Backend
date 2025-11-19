using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;
using PickleBall.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace PickleBall.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMoMoPaymentService _moMoService;
    private readonly IVnPayPaymentService _vnPayService;
    private readonly IOrderService _orderService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        ApplicationDbContext context,
        IMoMoPaymentService moMoService,
        IVnPayPaymentService vnPayService,
        IOrderService orderService,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _moMoService = moMoService;
        _vnPayService = vnPayService;
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
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid);

                    if (isPaid)
                    {
                        // Save payment record
                        var payment = new Payment
                        {
                            OrderId = order.Id,
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
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid);

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
                    await _orderService.UpdateOrderPaymentStatusAsync(order.Id, transactionId, isPaid);

                    if (isPaid)
                    {
                        var existingPayment = await _context.Payments
                            .FirstOrDefaultAsync(p => p.OrderId == order.Id && p.TransactionId == transactionId);

                        if (existingPayment == null)
                        {
                            var payment = new Payment
                            {
                                OrderId = order.Id,
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
}
