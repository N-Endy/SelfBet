namespace SelfBet.Domain.Entities;

/// <summary>
/// Per-team Dixon-Coles attack/defence parameters fitted from historical matches.
/// Stored per league so that a team's relative strength is normalised within its
/// league rather than compared cross-competition.
/// </summary>
public sealed class TeamStrength
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string League { get; init; }
    public required string Team { get; init; }
    public double Attack { get; init; }
    public double Defence { get; init; }
    public int SampleSize { get; init; }
    public DateTimeOffset FittedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// League-level aggregates needed to convert per-team attack/defence values into
/// per-fixture goal expectations: <c>λ_home = AvgHomeGoals × home.Attack × away.Defence</c>.
/// </summary>
public sealed class LeagueStrengthProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string League { get; init; }
    public double AvgHomeGoals { get; init; }
    public double AvgAwayGoals { get; init; }
    public double HomeAdvantage { get; init; }
    public double DixonColesRho { get; init; }
    public int SampleSize { get; init; }
    public DateTimeOffset FittedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
