namespace PickleBall.Application.DTOs.Payments;

/// <summary>
/// Response from VietQR API
/// </summary>
public class VietQRApiResponse
{
    public string Code { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public VietQRData? Data { get; set; }
}

public class VietQRData
{
    public int AcpId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string QrDataURL { get; set; } = string.Empty;
}

/// <summary>
/// DTO for returning QR payment info to frontend
/// </summary>
public class BankTransferPaymentResponse
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    
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
    
    public bool IsBankTransfer { get; set; } = true;
}

