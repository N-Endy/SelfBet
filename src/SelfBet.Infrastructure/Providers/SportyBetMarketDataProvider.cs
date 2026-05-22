using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;

namespace SelfBet.Infrastructure.Providers;

/// <summary>
/// Serves upcoming fixture odds from the shared <see cref="ISportyBetFixtureCache"/>.
/// </summary>
public sealed class SportyBetMarketDataProvider(
    ISportyBetFixtureCache fixtureCache,
    ILogger<SportyBetMarketDataProvider> logger) : IFootballDataProvider
{
    public async Task<IReadOnlyList<FixtureOddsDto>> GetUpcomingFixturesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var snapshot = await fixtureCache.GetSnapshotAsync(cancellationToken);
        var fixtures = snapshot.OddsFixtures
            .Where(f => f.KickoffUtc >= fromUtc && f.KickoffUtc <= toUtc)
            .ToList();

        logger.LogInformation(
            "SportyBetMarketDataProvider: {Count} fixtures in window [{From:u} .. {To:u}].",
            fixtures.Count, fromUtc, toUtc);

        return fixtures;
    }
}
