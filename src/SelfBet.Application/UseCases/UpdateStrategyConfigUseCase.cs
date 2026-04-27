using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Application.UseCases;

public sealed class UpdateStrategyConfigUseCase(
    IStrategyConfigRepository strategyConfigRepository,
    IAuditService auditService)
{
    public async Task<StrategyConfig> ExecuteAsync(StrategyConfig update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        update.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await strategyConfigRepository.SaveAsync(update, cancellationToken);
        await auditService.LogAsync(
            "config.updated",
            "Strategy configuration updated",
            new
            {
                update.OddsRange,
                update.SlipsPerDay,
                update.StakePercentagePerSlip,
                update.AutomationEnabled
            },
            cancellationToken);
        return update;
    }
}
