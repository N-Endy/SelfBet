namespace SelfBet.Infrastructure.Services;

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string To { get; set; } = "";
    public string DashboardUrl { get; set; } = "http://localhost:8090";
}
