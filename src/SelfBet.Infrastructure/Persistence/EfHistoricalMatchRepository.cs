using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class EfHistoricalMatchRepository(SelfBetDbContext db) : IHistoricalMatchRepository
{
    public async Task UpsertManyAsync(IEnumerable<HistoricalMatch> matches, CancellationToken ct)
    {
        var list = matches.ToList();
        if (list.Count == 0) return;

        var providerIds = list.Select(m => m.ProviderFixtureId).ToHashSet();
        var existing = await db.HistoricalMatches
            .Where(m => providerIds.Contains(m.ProviderFixtureId))
            .ToDictionaryAsync(m => m.ProviderFixtureId, ct);

        foreach (var m in list)
        {
            if (existing.TryGetValue(m.ProviderFixtureId, out var current))
            {
                db.Entry(current).CurrentValues.SetValues(new
                {
                    m.HomeGoals,
                    m.AwayGoals,
                    m.KickoffUtc,
                    CapturedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                db.HistoricalMatches.Add(m);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<HistoricalMatch>> GetByLeagueAsync(string league, int maxAgeDays, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-maxAgeDays);
        return await db.HistoricalMatches
            .AsNoTracking()
            .Where(m => m.League == league && m.KickoffUtc >= cutoff)
            .OrderByDescending(m => m.KickoffUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDistinctLeaguesAsync(CancellationToken ct) =>
        await db.HistoricalMatches.AsNoTracking()
            .Select(m => m.League)
            .Distinct()
            .ToListAsync(ct);
}
