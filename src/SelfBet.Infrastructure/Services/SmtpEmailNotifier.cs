using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;

namespace SelfBet.Infrastructure.Services;

public sealed class SmtpEmailNotifier(
    IOptions<SmtpOptions> opts,
    ILogger<SmtpEmailNotifier> logger) : IEmailNotifier
{
    private readonly SmtpOptions _opts = opts.Value;

    public async Task SendRunSummaryAsync(RunSummaryEmail summary, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
        {
            logger.LogInformation("SMTP disabled — skipping run summary email.");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SelfBet", _opts.Username));
        message.To.Add(MailboxAddress.Parse(_opts.To));
        message.Subject = $"SelfBet — {summary.SlipCount} slip{(summary.SlipCount == 1 ? "" : "s")} ready ({summary.RunDate:ddd d MMM})";

        var body = BuildBody(summary);
        message.Body = new TextPart("html") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_opts.Host, _opts.Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_opts.Username, _opts.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            logger.LogInformation("Run summary email sent to {To}", _opts.To);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send run summary email");
        }
    }

    private static string BuildBody(RunSummaryEmail s)
    {
        var slipRows = new System.Text.StringBuilder();
        foreach (var slip in s.Slips)
        {
            var legRows = string.Join("", slip.Legs.Select(l =>
                $"<tr><td style='padding:4px 8px'>{l.MatchTitle}</td>" +
                $"<td style='padding:4px 8px'>{l.League}</td>" +
                $"<td style='padding:4px 8px'>{l.Market} / {l.Outcome}</td>" +
                $"<td style='padding:4px 8px;text-align:right'>{l.Odds:F2}</td></tr>"));

            var codeBlock = !string.IsNullOrEmpty(slip.BookingCode)
                ? $@"<div style='margin:8px 0;padding:12px;background:#1a1a2e;border-radius:6px;text-align:center'>
                       <span style='font-size:28px;font-weight:bold;letter-spacing:6px;color:#eee'>{slip.BookingCode}</span><br/>
                       <a href='{slip.BookingUrl}' style='color:#4fc3f7;font-size:13px'>Open in SportyBet app →</a>
                     </div>"
                : "";

            slipRows.Append($@"
<div style='margin:16px 0;border:1px solid #333;border-radius:8px;overflow:hidden'>
  <div style='background:#0d1117;padding:10px 16px;display:flex;justify-content:space-between'>
    <strong style='color:#eee'>Slip {slip.Sequence} — {slip.TotalOdds:F2}× odds</strong>
    <span style='color:#aaa'>Stake: ₦{slip.Stake:N0} → Potential: ₦{slip.PotentialReturn:N0}</span>
  </div>
  {codeBlock}
  <table style='width:100%;border-collapse:collapse;font-size:13px'>
    <thead><tr style='background:#161b22;color:#aaa'>
      <th style='padding:4px 8px;text-align:left'>Match</th>
      <th style='padding:4px 8px;text-align:left'>League</th>
      <th style='padding:4px 8px;text-align:left'>Pick</th>
      <th style='padding:4px 8px;text-align:right'>Odds</th>
    </tr></thead>
    <tbody style='color:#ddd'>{legRows}</tbody>
  </table>
</div>");
        }

        return $@"<!DOCTYPE html><html><head><meta charset='UTF-8'/></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,sans-serif;background:#0d1117;color:#ddd;padding:24px;max-width:700px;margin:0 auto'>
  <h2 style='color:#eee;margin-bottom:4px'>SelfBet — Daily Slips</h2>
  <p style='color:#888;margin-top:0'>{s.RunDate:dddd, d MMMM yyyy} · Balance: ₦{s.Balance:N0}</p>
  {slipRows}
  <p style='color:#555;font-size:12px;margin-top:24px'>
    Generated at {s.GeneratedAtUtc:HH:mm} UTC · 
    <a href='{s.DashboardUrl}' style='color:#4fc3f7'>Open Dashboard</a>
  </p>
</body></html>";
    }
}
