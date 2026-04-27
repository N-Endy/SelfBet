using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Domain.Entities;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.Services;

/// <summary>
/// Target-aware greedy optimizer that builds N disjoint slips. For each slip it
/// repeatedly picks the +EV candidate whose odds bring the running product closest
/// to the target (midpoint of the odds range) without overshooting the maximum.
/// Matches consumed by an attempted slip stay reserved so slips never duplicate.
/// </summary>
public sealed class SlipOptimizer : ISlipOptimizer
{
    public SlipBuildResult Build(
        IReadOnlyList<CandidateBet> candidates,
        StrategyConfig config,
        Guid runId,
        decimal stakePerSlip)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(config);

        var pool = candidates
            .Where(c => c.Edge >= config.MinEdgeThreshold)
            .Where(c => c.Odds >= config.MinLegOdds && c.Odds <= config.MaxLegOdds)
            .OrderByDescending(c => c.ExpectedValue)
            .ToList();

        var slips = new List<Slip>();
        var usedMatches = new HashSet<Guid>();
        var notes = new List<string>();

        for (var i = 1; i <= config.SlipsPerDay; i++)
        {
            var slip = BuildSlip(pool, config, runId, i, stakePerSlip, usedMatches);
            slips.Add(slip);

            if (slip.Status == SlipStatus.Failed)
            {
                notes.Add($"Slip {i}: {slip.FailureReason}");
            }
        }

        return new SlipBuildResult
        {
            Slips = slips,
            Candidates = pool,
            Notes = notes.Count == 0 ? null : string.Join(" | ", notes)
        };
    }

    private static Slip BuildSlip(
        IReadOnlyList<CandidateBet> pool,
        StrategyConfig config,
        Guid runId,
        int sequence,
        decimal stakePerSlip,
        HashSet<Guid> usedMatches)
    {
        var slip = new Slip
        {
            RunId = runId,
            RunDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Sequence = sequence,
            Stake = stakePerSlip,
            Status = SlipStatus.Planned
        };

        var pickedMatches = new HashSet<Guid>();
        var target = (config.OddsRange.Min + config.OddsRange.Max) / 2m;
        decimal totalOdds = 1m;

        while (slip.Legs.Count < config.MaxLegsPerSlip)
        {
            var remaining = pool
                .Where(c => !usedMatches.Contains(c.Match.Id))
                .Where(c => !pickedMatches.Contains(c.Match.Id))
                .Select(c =>
                {
                    var prospective = Math.Round(totalOdds * c.Odds, 4);
                    return new
                    {
                        Candidate = c,
                        Prospective = prospective,
                        ExceedsMax = prospective > config.OddsRange.Max,
                        InRange = config.OddsRange.Contains(prospective),
                        DistanceToTarget = Math.Abs(target - prospective)
                    };
                })
                .Where(c => !c.ExceedsMax)
                .ToList();

            if (remaining.Count == 0)
            {
                break;
            }

            var pick = remaining
                .OrderByDescending(c => c.InRange)
                .ThenBy(c => c.DistanceToTarget)
                .ThenByDescending(c => c.Candidate.ExpectedValue)
                .First();

            slip.Legs.Add(new SlipLeg
            {
                SlipId = slip.Id,
                MatchId = pick.Candidate.Match.Id,
                MatchTitle = pick.Candidate.Match.Title,
                League = pick.Candidate.Match.League,
                Market = pick.Candidate.Market,
                Outcome = pick.Candidate.Outcome,
                Odds = pick.Candidate.Odds,
                ModelProbability = pick.Candidate.ModelProbability,
                KickoffUtc = pick.Candidate.Match.KickoffUtc
            });

            pickedMatches.Add(pick.Candidate.Match.Id);
            totalOdds = pick.Prospective;

            if (slip.Legs.Count >= config.MinLegsPerSlip && config.OddsRange.Contains(totalOdds))
            {
                break;
            }
        }

        slip.TotalOdds = Math.Round(totalOdds, 2);

        if (slip.Legs.Count >= config.MinLegsPerSlip && config.OddsRange.Contains(slip.TotalOdds))
        {
            slip.Status = SlipStatus.Ready;
            foreach (var matchId in pickedMatches)
            {
                usedMatches.Add(matchId);
            }
        }
        else
        {
            slip.Status = SlipStatus.Failed;
            slip.FailureReason = slip.Legs.Count == 0
                ? "No +EV candidates available."
                : $"Could not reach odds range [{config.OddsRange.Min:F2}, {config.OddsRange.Max:F2}] with disjoint legs.";

            foreach (var matchId in pickedMatches)
            {
                usedMatches.Add(matchId);
            }
        }

        return slip;
    }
}
