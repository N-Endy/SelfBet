namespace SelfBet.Application.Models;

public sealed class FeatureVector
{
    public required string FixtureId { get; init; }
    public required string Market { get; init; }
    public required string Outcome { get; init; }
    public decimal MarketImpliedProbability { get; init; }
    public decimal AttackStrengthDelta { get; init; }
    public decimal FormDelta { get; init; }
    public decimal RestDaysDelta { get; init; }
    public decimal InjuriesPenalty { get; init; }
    public decimal MarketDispersion { get; init; }
}
