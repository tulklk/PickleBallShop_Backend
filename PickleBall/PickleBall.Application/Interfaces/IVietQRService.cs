using PickleBall.Application.DTOs.Payments;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface IVietQRService
{
    /// <summary>
    /// Generate VietQR code for bank transfer payment
    /// </summary>
    /// <param name="order">Order to generate QR for</param>
    /// <returns>Bank transfer payment response with QR code</returns>
    Task<BankTransferPaymentResponse> GenerateQRCodeAsync(Order order);
}

