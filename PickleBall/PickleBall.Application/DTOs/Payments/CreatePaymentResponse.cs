namespace PickleBall.Application.DTOs.Payments;

public class CreatePaymentResponse
{
    public string? PaymentUrl { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public bool IsCod { get; set; } = false;
    
    // Bank Transfer (VietQR) specific fields
    public bool IsBankTransfer { get; set; } = false;
    public BankTransferInfo? BankTransferInfo { get; set; }
}

public class BankTransferInfo
{
    // QR Code image as Data URI (can be displayed directly in <img> tag)
    public string QrDataUrl { get; set; } = string.Empty;
    
    // QR Code as text (for apps that can decode QR text)
    public string QrCode { get; set; } = string.Empty;
    
    // Bank transfer information for manual transfer
    public string BankName { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = string.Empty;
}
