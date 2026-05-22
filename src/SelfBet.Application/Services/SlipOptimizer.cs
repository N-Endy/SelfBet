using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Domain.Entities;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.Services;

/// <summary>
/// Beam-search optimizer that builds N disjoint slips targeting the configured odds range.
/// </summary>
public sealed class SlipOptimizer : ISlipOptimizer
{
    private const decimal DiversifyBonusPerLeg = 15m;

    public SlipBuildResult Build(
        IReadOnlyList<CandidateBet> candidates,
        StrategyConfig config,
        Guid runId,
        decimal stakePerSlip)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(config);

        var pool = PreparePool(candidates, config);
        var slips = new List<Slip>();
        var usedMatches = new HashSet<Guid>();
        var notes = new List<string>();

        for (var i = 1; i <= config.SlipsPerDay; i++)
        {
            var slip = BuildSlipBeam(pool, config, runId, i, stakePerSlip, usedMatches);
            slips.Add(slip);

            if (slip.Status == SlipStatus.Failed)
                notes.Add($"Slip {i}: {slip.FailureReason}");
        }

        return new SlipBuildResult
        {
            Slips = slips,
            Candidates = pool,
            Notes = notes.Count == 0 ? null : string.Join(" | ", notes)
        };
    }

    private static List<CandidateBet> PreparePool(IReadOnlyList<CandidateBet> candidates, StrategyConfig config)
    {
        var filtered = candidates
            .Where(c => c.Edge >= config.MinEdgeThreshold)
            .Where(c => c.Odds >= config.MinLegOdds && c.Odds <= config.MaxLegOdds)
            .Where(c => c.ModelProbability > c.ImpliedProbability)
            .Where(c => config.MinModelProbability <= 0 || c.ModelProbability >= config.MinModelProbability)
            .ToList();

        return filtered
            .GroupBy(c => c.Match.Id)
            .Select(g => g
                .OrderByDescending(c => c.ExpectedValue)
                .ThenByDescending(c => c.Edge)
                .ThenByDescending(c => c.PredictionSource == "DixonColes" ? 1 : 0)
                .First())
            .OrderByDescending(c => c.ExpectedValue)
            .ToList();
    }

    private static Slip BuildSlipBeam(
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

        var available = pool.Where(c => !usedMatches.Contains(c.Match.Id)).ToList();
        if (available.Count == 0)
        {
            slip.Status = SlipStatus.Failed;
            slip.FailureReason = "No +EV candidates available.";
            return slip;
        }

        var target = (config.OddsRange.Min + config.OddsRange.Max) / 2m;
        var beamWidth = Math.Max(4, config.OptimizerBeamWidth);
        var best = SearchBestSlip(available, config, target, beamWidth);

        if (best is null)
        {
            slip.Status = SlipStatus.Failed;
            slip.FailureReason = $"Could not reach odds range [{config.OddsRange.Min:F2}, {config.OddsRange.Max:F2}] with disjoint legs.";
            return slip;
        }

        foreach (var leg in best.Legs)
        {
            slip.Legs.Add(new SlipLeg
            {
                SlipId = slip.Id,
                MatchId = leg.Match.Id,
                MatchTitle = leg.Match.Title,
                League = leg.Match.League,
                Market = leg.Market,
                Outcome = leg.Outcome,
                Odds = leg.Odds,
                ModelProbability = leg.ModelProbability,
                MarketImpliedProbability = leg.ImpliedProbability,
                Edge = leg.Edge,
                ExpectedValue = leg.ExpectedValue,
                PredictionSource = leg.PredictionSource,
                HomeSampleSize = leg.HomeSampleSize,
                AwaySampleSize = leg.AwaySampleSize,
                KickoffUtc = leg.Match.KickoffUtc
            });
        }

        slip.TotalOdds = Math.Round(best.TotalOdds, 2);
        slip.Status = SlipStatus.Ready;

        foreach (var leg in best.Legs)
            usedMatches.Add(leg.Match.Id);

        return slip;
    }

    private static BeamState? SearchBestSlip(
        IReadOnlyList<CandidateBet> available,
        StrategyConfig config,
        decimal target,
        int beamWidth)
    {
        var beams = new List<BeamState> { new([], 1m, new HashSet<Guid>()) };
        BeamState? bestFeasible = null;
        var bestScore = decimal.MinValue;

        for (var depth = 0; depth < config.MaxLegsPerSlip; depth++)
        {
            var nextBeams = new List<BeamState>();

            foreach (var beam in beams)
            {
                foreach (var candidate in available)
                {
                    if (beam.UsedMatches.Contains(candidate.Match.Id)) continue;

                    var prospective = Math.Round(beam.TotalOdds * candidate.Odds, 4);
                    if (prospective > config.OddsRange.Max) continue;

                    if (!CanReachMinOdds(prospective, config, beam.Legs.Count + 1, available, beam.UsedMatches, candidate.Match.Id))
                        continue;

                    var legs = beam.Legs.Append(candidate).ToList();
                    var used = new HashSet<Guid>(beam.UsedMatches) { candidate.Match.Id };
                    var state = new BeamState(legs, prospective, used);
                    var score = ScoreState(state, config, target);

                    if (state.Legs.Count >= config.MinLegsPerSlip && config.OddsRange.Contains(state.TotalOdds))
                    {
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestFeasible = state;
                        }
                    }

                    nextBeams.Add(state);
                }
            }

            if (nextBeams.Count == 0) break;

            beams = nextBeams
                .OrderByDescending(s => ScoreState(s, config, target))
                .Take(beamWidth)
                .ToList();
        }

        return bestFeasible;
    }

    private static bool CanReachMinOdds(
        decimal currentProduct,
        StrategyConfig config,
        int legsSoFar,
        IReadOnlyList<CandidateBet> available,
        HashSet<Guid> used,
        Guid justAdded)
    {
        if (currentProduct >= config.OddsRange.Min) return true;

        var remainingLegs = config.MaxLegsPerSlip - legsSoFar;
        if (remainingLegs <= 0) return false;

        var unusedOdds = available
            .Where(c => !used.Contains(c.Match.Id) && c.Match.Id != justAdded)
            .Select(c => c.Odds)
            .OrderByDescending(o => o)
            .Take(remainingLegs)
            .ToList();

        if (unusedOdds.Count == 0) return false;

        var maxProduct = currentProduct;
        foreach (var o in unusedOdds)
            maxProduct *= o;

        return maxProduct >= config.OddsRange.Min;
    }

    private static decimal ScoreState(BeamState state, StrategyConfig config, decimal target)
    {
        var inRange = config.OddsRange.Contains(state.TotalOdds);
        var distance = Math.Abs(target - state.TotalOdds);
        var sumEv = state.Legs.Sum(l => l.ExpectedValue);
        var minEdge = state.Legs.Count == 0 ? 0 : state.Legs.Min(l => l.Edge);
        var diversify = config.PreferDiversification && inRange && state.Legs.Count >= 3
            ? (state.Legs.Count - 2) * DiversifyBonusPerLeg
            : 0m;

        return (inRange ? 10_000m : 0m)
               - distance * 10m
               + sumEv * 100m
               + minEdge * 50m
               + diversify;
    }

    private sealed record BeamState(
        IReadOnlyList<CandidateBet> Legs,
        decimal TotalOdds,
        HashSet<Guid> UsedMatches);
}
