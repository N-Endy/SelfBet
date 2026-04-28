namespace SelfBet.Domain.Entities;

public sealed class CandidateBet
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Match Match { get; init; }
    public required string Market { get; init; }
    public required string Outcome { get; init; }
    public decimal Odds { get; init; }
    public decimal ModelProbability { get; init; }
    public string PredictionSource { get; init; } = "BookmakerFallback";
    public int? HomeSampleSize { get; init; }
    public int? AwaySampleSize { get; init; }

    public decimal ImpliedProbability => Odds <= 0 ? 0m : Math.Round(1m / Odds, 4);
    public decimal Edge => Math.Round(ModelProbability - ImpliedProbability, 4);
    public decimal ExpectedValue => Math.Round((ModelProbability * Odds) - 1m, 4);
}
