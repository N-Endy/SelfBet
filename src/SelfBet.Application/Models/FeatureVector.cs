using SelfBet.Application.Abstractions;

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

    public string League { get; init; } = string.Empty;
    public string HomeTeam { get; init; } = string.Empty;
    public string AwayTeam { get; init; } = string.Empty;

    /// <summary>
    /// Pre-computed fixture-level goal expectations from the team-strength
    /// service. <c>null</c> when the league has no fitted data — the prediction
    /// service then falls back to the bookmaker-derived heuristic.
    /// </summary>
    public FixtureExpectation? FixtureExpectation { get; init; }
}
