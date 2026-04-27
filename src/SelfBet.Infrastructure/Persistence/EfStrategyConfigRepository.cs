using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class EfStrategyConfigRepository(SelfBetDbContext db) : IStrategyConfigRepository
{
    public async Task<StrategyConfig> GetAsync(CancellationToken ct = default)
    {
        var config = await db.StrategyConfigs.FirstOrDefaultAsync(ct);
        if (config is null)
        {
            config = new StrategyConfig();
            db.StrategyConfigs.Add(config);
            await db.SaveChangesAsync(ct);
        }

        return config;
    }

    public async Task SaveAsync(StrategyConfig config, CancellationToken ct = default)
    {
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var existing = await db.StrategyConfigs.FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            config.Id = 1;
            db.StrategyConfigs.Add(config);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(config);
        }

        await db.SaveChangesAsync(ct);
    }
}
