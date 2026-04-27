using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

/// <summary>
/// Computes and serves per-team Dixon-Coles attack/defence ratings, plus the
/// league aggregates needed to turn them into per-fixture goal expectations.
/// </summary>
public interface ITeamStrengthService
{
    /// <summary>
    /// Refit all leagues currently present in the historical-matches repository.
    /// Called by a daily background worker.
    /// </summary>
    Task RefitAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Refit a single league and persist the resulting strengths. Returns the
    /// number of teams fitted (0 when sample size is too small).
    /// </summary>
    Task<int> RefitLeagueAsync(string league, CancellationToken cancellationToken);

    /// <summary>
    /// Get the cached fixture-level goal expectations (λ_home, λ_away) for
    /// <paramref name="homeTeam"/> vs <paramref name="awayTeam"/> in <paramref name="league"/>.
    /// Returns <c>null</c> when there is insufficient data to publish a fit.
    /// </summary>
    Task<FixtureExpectation?> GetFixtureExpectationAsync(
        string league,
        string homeTeam,
        string awayTeam,
        CancellationToken cancellationToken);
}

public sealed record FixtureExpectation(
    double LambdaHome,
    double LambdaAway,
    double DixonColesRho,
    int HomeSampleSize,
    int AwaySampleSize);

public interface ITeamStrengthRepository
{
    Task<TeamStrength?> GetAsync(string league, string team, CancellationToken cancellationToken);
    Task<LeagueStrengthProfile?> GetLeagueProfileAsync(string league, CancellationToken cancellationToken);
    Task UpsertManyAsync(
        IEnumerable<TeamStrength> teams,
        LeagueStrengthProfile leagueProfile,
        CancellationToken cancellationToken);
}
