using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.UseCases;

public sealed class PerformanceQuery(ISlipRepository slipRepository)
{
    public async Task<PerformanceSummary> GetSummaryAsync(int rangeDays, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, rangeDays));
        var slips = await slipRepository.GetRecentAsync(500, cancellationToken);
        var inRange = slips.Where(s => s.CreatedAtUtc >= cutoff).ToList();

        var totalStake = inRange.Sum(s => s.Stake);
        var totalReturn = inRange.Sum(s => s.Payout);
        var won = inRange.Count(s => s.Status == SlipStatus.Won);
        var lost = inRange.Count(s => s.Status == SlipStatus.Lost);
        var pending = inRange.Count(s => s.Status is SlipStatus.Placed or SlipStatus.AwaitingConfirmation or SlipStatus.Ready);

        return new PerformanceSummary
        {
            TotalSlips = inRange.Count,
            Won = won,
            Lost = lost,
            Pending = pending,
            TotalStake = totalStake,
            TotalReturn = totalReturn
        };
    }
}
