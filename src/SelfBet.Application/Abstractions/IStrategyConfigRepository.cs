using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IStrategyConfigRepository
{
    Task<StrategyConfig> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(StrategyConfig config, CancellationToken cancellationToken);
}
