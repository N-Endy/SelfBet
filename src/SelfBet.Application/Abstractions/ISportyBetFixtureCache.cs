using SelfBet.Application.Models;

namespace SelfBet.Application.Abstractions;

public interface ISportyBetFixtureCache
{
    Task<SportyBetFixtureSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
