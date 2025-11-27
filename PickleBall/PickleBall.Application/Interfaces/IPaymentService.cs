using PickleBall.Application.DTOs.Payments;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string cancelUrl, string notifyUrl);
}

public interface IMoMoPaymentService
{
    Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string cancelUrl, string notifyUrl);
    Task<bool> VerifyCallbackAsync(Dictionary<string, string> callbackData);
}

public interface IVnPayPaymentService
{
    Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string cancelUrl, string notifyUrl);
    Task<bool> VerifyCallbackAsync(Dictionary<string, string> callbackData);
}
