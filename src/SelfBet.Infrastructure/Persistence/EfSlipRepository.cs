using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class EfSlipRepository(SelfBetDbContext db) : ISlipRepository
{
    public async Task SaveAsync(IReadOnlyCollection<Slip> slips, CancellationToken ct = default)
    {
        foreach (var slip in slips)
        {
            if (db.Entry(slip).State == EntityState.Detached)
                db.Slips.Add(slip);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Slip slip, CancellationToken ct = default)
    {
        if (db.Entry(slip).State == EntityState.Detached)
            db.Slips.Attach(slip);

        db.Entry(slip).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Slip?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Slips
            .Include(s => s.Legs)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Slip>> GetByRunAsync(Guid runId, CancellationToken ct = default) =>
        await db.Slips
            .Include(s => s.Legs)
            .Where(s => s.RunId == runId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Slip>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        await db.Slips
            .Include(s => s.Legs)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Slip>> GetByDateAsync(DateOnly date, CancellationToken ct = default) =>
        await db.Slips
            .Include(s => s.Legs)
            .Where(s => s.RunDate == date)
            .ToListAsync(ct);
}
