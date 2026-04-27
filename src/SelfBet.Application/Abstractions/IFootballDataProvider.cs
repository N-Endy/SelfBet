using SelfBet.Application.Models;

namespace SelfBet.Application.Abstractions;

public interface IFootballDataProvider
{
    Task<IReadOnlyList<FixtureOddsDto>> GetUpcomingFixturesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}
