using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IMoMoPaymentService _moMoService;
    private readonly IVnPayPaymentService _vnPayService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IMoMoPaymentService moMoService,
        IVnPayPaymentService vnPayService,
        ILogger<PaymentService> logger)
    {
        _moMoService = moMoService;
        _vnPayService = vnPayService;
        _logger = logger;
    }

    public async Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string notifyUrl)
    {
        if (!order.PaymentMethod.HasValue)
        {
            throw new InvalidOperationException("Payment method not specified");
        }

        return order.PaymentMethod.Value switch
        {
            PaymentMethod.MoMo => await _moMoService.CreatePaymentAsync(order, returnUrl, notifyUrl),
            PaymentMethod.VNPay => await _vnPayService.CreatePaymentAsync(order, returnUrl, notifyUrl),
            _ => throw new NotSupportedException($"Payment method {order.PaymentMethod.Value} is not supported")
        };
    }
}
