using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class EfBankrollRepository(SelfBetDbContext db) : IBankrollRepository
{
    public async Task<BankrollSnapshot?> GetLatestAsync(CancellationToken ct = default) =>
        await db.BankrollSnapshots
            .OrderByDescending(s => s.CapturedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task SaveAsync(BankrollSnapshot snapshot, CancellationToken ct = default)
    {
        db.BankrollSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BankrollSnapshot>> GetHistoryAsync(int limit, CancellationToken ct = default) =>
        await db.BankrollSnapshots
            .OrderByDescending(s => s.CapturedAtUtc)
            .Take(limit)
            .ToListAsync(ct);
}
