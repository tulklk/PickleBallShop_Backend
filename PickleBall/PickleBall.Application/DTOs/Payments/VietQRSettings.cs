namespace PickleBall.Application.DTOs.Payments;

public class VietQRSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.vietqr.io/v2/generate";
    
    // Bank account information
    public string AccountNo { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public int AcqId { get; set; } // Bank BIN code (e.g., 970415 for Vietinbank, 970436 for Vietcombank)
    
    // Template options: compact2 (540x640), compact (540x540), qr_only (480x480), print (600x776)
    public string Template { get; set; } = "compact2";
}

