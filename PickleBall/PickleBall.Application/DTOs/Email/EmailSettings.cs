namespace PickleBall.Application.DTOs.Email;

public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "PickleBall Shop";
    public bool EnableSsl { get; set; } = true;
    public string FrontendUrl { get; set; } = "https://pickleball-shop-frontend.vercel.app";
}

