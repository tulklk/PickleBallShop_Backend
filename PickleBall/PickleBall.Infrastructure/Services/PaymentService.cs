using Microsoft.Extensions.Logging;
using PickleBall.Application.DTOs.Payments;
using PickleBall.Application.Interfaces;
using PickleBall.Domain.Entities;

namespace PickleBall.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IMoMoPaymentService _moMoService;
    private readonly IVnPayPaymentService _vnPayService;
    private readonly IVietQRService _vietQRService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IMoMoPaymentService moMoService,
        IVnPayPaymentService vnPayService,
        IVietQRService vietQRService,
        ILogger<PaymentService> logger)
    {
        _moMoService = moMoService;
        _vnPayService = vnPayService;
        _vietQRService = vietQRService;
        _logger = logger;
    }

    public async Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string notifyUrl)
    {
        if (!order.PaymentMethod.HasValue)
        {
            throw new InvalidOperationException("Payment method not specified");
        }

        // COD doesn't require payment gateway, return response with empty payment URL
        if (order.PaymentMethod.Value == PaymentMethod.COD)
        {
            return new CreatePaymentResponse
            {
                PaymentUrl = null, // No payment URL for COD
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                IsCod = true
            };
        }

        // Bank Transfer uses VietQR to generate QR code
        if (order.PaymentMethod.Value == PaymentMethod.BankTransfer)
        {
            var vietQRResponse = await _vietQRService.GenerateQRCodeAsync(order);
            return new CreatePaymentResponse
            {
                PaymentUrl = null, // No redirect URL for bank transfer
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                IsBankTransfer = true,
                BankTransferInfo = new BankTransferInfo
                {
                    QrDataUrl = vietQRResponse.QrDataUrl,
                    QrCode = vietQRResponse.QrCode,
                    BankName = vietQRResponse.BankName,
                    AccountNo = vietQRResponse.AccountNo,
                    AccountName = vietQRResponse.AccountName,
                    Amount = vietQRResponse.Amount,
                    TransferContent = vietQRResponse.TransferContent
                }
            };
        }

        return order.PaymentMethod.Value switch
        {
            PaymentMethod.MoMo => await _moMoService.CreatePaymentAsync(order, returnUrl, notifyUrl),
            PaymentMethod.VNPay => await _vnPayService.CreatePaymentAsync(order, returnUrl, notifyUrl),
            _ => throw new NotSupportedException($"Payment method {order.PaymentMethod.Value} is not supported")
        };
    }
}
