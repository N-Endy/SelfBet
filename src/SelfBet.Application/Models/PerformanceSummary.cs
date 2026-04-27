namespace SelfBet.Application.Models;

public sealed class PerformanceSummary
{
    public int TotalSlips { get; init; }
    public int Won { get; init; }
    public int Lost { get; init; }
    public int Pending { get; init; }
    public decimal TotalStake { get; init; }
    public decimal TotalReturn { get; init; }
    public decimal NetProfit => Math.Round(TotalReturn - TotalStake, 2);
    public decimal Roi => TotalStake == 0 ? 0 : Math.Round(NetProfit / TotalStake, 4);
    public decimal HitRate => TotalSlips == 0 ? 0 : Math.Round((decimal)Won / TotalSlips, 4);
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
