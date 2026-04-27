using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Application.UseCases;

public sealed class ToggleAutomationUseCase(
    IStrategyConfigRepository strategyConfigRepository,
    IAuditService auditService)
{
    public async Task<StrategyConfig> ExecuteAsync(bool enable, CancellationToken cancellationToken)
    {
        var config = await strategyConfigRepository.GetAsync(cancellationToken);
        config.AutomationEnabled = enable;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await strategyConfigRepository.SaveAsync(config, cancellationToken);
        await auditService.LogAsync(
            enable ? "automation.started" : "automation.stopped",
            enable ? "Automation enabled" : "Automation disabled",
            null,
            cancellationToken);
        return config;
    }
}
