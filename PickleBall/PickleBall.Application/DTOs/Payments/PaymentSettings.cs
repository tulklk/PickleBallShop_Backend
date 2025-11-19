namespace PickleBall.Application.DTOs.Payments;

public class MoMoPaymentSettings
{
    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string IpnUrl { get; set; } = string.Empty;
}

public class VnPayPaymentSettings
{
    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string IpnUrl { get; set; } = string.Empty;
}
