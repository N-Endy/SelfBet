using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Application.Services;

public sealed class BankrollService(IBankrollRepository repository) : IBankrollService
{
    public async Task<BankrollSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var snapshot = await repository.GetLatestAsync(cancellationToken);
        if (snapshot is not null)
        {
            return snapshot;
        }

        var bootstrap = new BankrollSnapshot
        {
            Balance = 10_000m,
            StakePerSlip = 200m,
            Currency = "NGN",
            Note = "Initial bootstrap snapshot"
        };
        await repository.SaveAsync(bootstrap, cancellationToken);
        return bootstrap;
    }

    public async Task<BankrollSnapshot> CaptureAsync(
        decimal balance,
        decimal stakePerSlip,
        string? note,
        CancellationToken cancellationToken)
    {
        var snapshot = new BankrollSnapshot
        {
            Balance = balance,
            StakePerSlip = stakePerSlip,
            Note = note
        };
        await repository.SaveAsync(snapshot, cancellationToken);
        return snapshot;
    }

    public decimal ComputeStakePerSlip(decimal balance, StrategyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var raw = balance * config.StakePercentagePerSlip;
        var increment = config.StakeIncrement <= 0 ? 1m : config.StakeIncrement;
        var rounded = Math.Floor(raw / increment) * increment;
        return Math.Max(rounded, 0m);
    }
}
