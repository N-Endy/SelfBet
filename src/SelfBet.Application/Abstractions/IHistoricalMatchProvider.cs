using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

/// <summary>
/// Pulls finished league matches from an external provider (API-Football) for
/// fitting team-strength models. Implementations are expected to be tolerant of
/// partial responses — they should return whatever they could fetch and log the
/// rest.
/// </summary>
public interface IHistoricalMatchProvider
{
    /// <summary>
    /// Returns finished matches in <paramref name="league"/> for the given seasons.
    /// </summary>
    Task<IReadOnlyList<HistoricalMatch>> FetchAsync(
        string league,
        IReadOnlyList<string> seasons,
        CancellationToken cancellationToken);

    /// <summary>True iff the provider is configured (has API key etc.) and ready to call.</summary>
    bool IsConfigured { get; }
}

public interface IHistoricalMatchRepository
{
    Task UpsertManyAsync(IEnumerable<HistoricalMatch> matches, CancellationToken cancellationToken);
    Task<IReadOnlyList<HistoricalMatch>> GetByLeagueAsync(string league, int maxAgeDays, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetDistinctLeaguesAsync(CancellationToken cancellationToken);
}
