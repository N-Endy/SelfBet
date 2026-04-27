using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IBankrollService
{
    Task<BankrollSnapshot> GetCurrentAsync(CancellationToken cancellationToken);
    Task<BankrollSnapshot> CaptureAsync(decimal balance, decimal stakePerSlip, string? note, CancellationToken cancellationToken);
    decimal ComputeStakePerSlip(decimal balance, StrategyConfig config);
}
