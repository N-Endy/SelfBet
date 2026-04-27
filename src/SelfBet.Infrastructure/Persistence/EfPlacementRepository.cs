using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class EfPlacementRepository(SelfBetDbContext db) : IPlacementRepository
{
    public async Task SaveAsync(PlacementAttempt attempt, CancellationToken ct = default)
    {
        db.PlacementAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PlacementAttempt>> GetBySlipAsync(Guid slipId, CancellationToken ct = default) =>
        await db.PlacementAttempts
            .Where(a => a.SlipId == slipId)
            .OrderByDescending(a => a.AttemptedAtUtc)
            .ToListAsync(ct);
}
