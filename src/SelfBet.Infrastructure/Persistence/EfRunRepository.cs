using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class EfRunRepository(SelfBetDbContext db) : IRunRepository
{
    public async Task<Run?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Runs.FindAsync([id], ct);

    public async Task SaveAsync(Run run, CancellationToken ct = default)
    {
        if (db.Entry(run).State == EntityState.Detached)
            db.Runs.Add(run);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Run>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        await db.Runs
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
}
