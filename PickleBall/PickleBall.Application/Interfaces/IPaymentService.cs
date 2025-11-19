using PickleBall.Application.DTOs.Payments;
using PickleBall.Domain.Entities;

namespace PickleBall.Application.Interfaces;

public interface IPaymentService
{
    Task<CreatePaymentResponse> CreatePaymentAsync(Order order, string returnUrl, string notifyUrl);
}

public interface IMoMoPaymentService : IPaymentService
{
    Task<bool> VerifyCallbackAsync(Dictionary<string, string> callbackData);
}

public interface IVnPayPaymentService : IPaymentService
{
    Task<bool> VerifyCallbackAsync(Dictionary<string, string> callbackData);
}
