namespace SelfBet.Application.Models;

public sealed class RunSummaryEmail
{
    public DateOnly RunDate { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public decimal Balance { get; init; }
    public int SlipCount { get; init; }
    public string DashboardUrl { get; init; } = "";
    public List<SlipEmailSummary> Slips { get; init; } = [];
}

public sealed class SlipEmailSummary
{
    public int Sequence { get; init; }
    public decimal TotalOdds { get; init; }
    public decimal Stake { get; init; }
    public decimal PotentialReturn { get; init; }
    public string? BookingCode { get; init; }
    public string? BookingUrl { get; init; }
    public List<LegEmailSummary> Legs { get; init; } = [];
}

public sealed class LegEmailSummary
{
    public string MatchTitle { get; init; } = "";
    public string League { get; init; } = "";
    public string Market { get; init; } = "";
    public string Outcome { get; init; } = "";
    public decimal Odds { get; init; }
}
