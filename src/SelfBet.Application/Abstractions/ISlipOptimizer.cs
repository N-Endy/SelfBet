using SelfBet.Application.Models;
using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface ISlipOptimizer
{
    SlipBuildResult Build(
        IReadOnlyList<CandidateBet> candidates,
        StrategyConfig config,
        Guid runId,
        decimal stakePerSlip);
}
