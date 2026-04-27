namespace SelfBet.Domain.Entities;

public sealed class MarketOdds
{
    public required string Market { get; init; }
    public required string Outcome { get; init; }
    public decimal Odds { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public decimal ImpliedProbability => Odds <= 0 ? 0m : Math.Round(1m / Odds, 4);
}
