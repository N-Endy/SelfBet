namespace SelfBet.Application.Models;

public sealed class FixtureOddsDto
{
    public required string FixtureId { get; init; }
    public required string League { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public DateTimeOffset KickoffUtc { get; init; }
    public List<MarketOddsDto> Markets { get; init; } = [];
    public TeamStatsDto? HomeStats { get; init; }
    public TeamStatsDto? AwayStats { get; init; }
}

public sealed class MarketOddsDto
{
    public required string Market { get; init; }
    public List<OutcomeOddsDto> Outcomes { get; init; } = [];
}

public sealed class OutcomeOddsDto
{
    public required string Outcome { get; init; }
    public decimal Odds { get; init; }
}

public sealed class TeamStatsDto
{
    public decimal Form { get; init; }
    public decimal RollingXg { get; init; }
    public decimal RollingXgAgainst { get; init; }
    public decimal HomeAwayStrength { get; init; }
    public int RestDays { get; init; }
    public int InjuriesKey { get; init; }
}
