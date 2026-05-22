using SelfBet.Application.Services;
using SelfBet.Domain.Entities;
using SelfBet.Domain.Enums;
using Xunit;

namespace SelfBet.Application.Tests;

public sealed class SlipOptimizerTests
{
    private static readonly SlipOptimizer Optimizer = new();

    [Fact]
    public void Build_uses_multiple_legs_when_two_legs_cannot_reach_odds_range()
    {
        var config = DefaultConfig();
        // 1.7^2 = 2.89 (below 6), 1.7^4 = 8.35 (in range) — cannot stop at two legs.
        var candidates = Enumerable.Range(1, 6)
            .Select(i => MakeCandidate($"m{i}", $"H{i}", $"A{i}", 1.7m, 0.65m))
            .ToList();

        var result = Optimizer.Build(candidates, config, Guid.NewGuid(), 100m);
        var slip = result.Slips.First(s => s.Sequence == 1);

        Assert.Equal(SlipStatus.Ready, slip.Status);
        Assert.True(slip.Legs.Count >= 4, $"Expected 4+ legs, got {slip.Legs.Count}");
        Assert.InRange(slip.TotalOdds, config.OddsRangeMin, config.OddsRangeMax);
    }

    [Fact]
    public void Build_disjoint_slips_do_not_share_matches()
    {
        var config = DefaultConfig();
        config.SlipsPerDay = 2;
        var candidates = Enumerable.Range(1, 8)
            .Select(i => MakeCandidate($"m{i}", $"H{i}", $"A{i}", 1.85m, 0.62m))
            .ToList();

        var result = Optimizer.Build(candidates, config, Guid.NewGuid(), 100m);

        Assert.Equal(SlipStatus.Ready, result.Slips[0].Status);
        Assert.Equal(SlipStatus.Ready, result.Slips[1].Status);

        var ids1 = result.Slips[0].Legs.Select(l => l.MatchId).ToHashSet();
        var ids2 = result.Slips[1].Legs.Select(l => l.MatchId).ToHashSet();
        Assert.Empty(ids1.Intersect(ids2));
    }

    [Fact]
    public void Build_rejects_total_odds_below_min()
    {
        var config = DefaultConfig();
        config.OddsRangeMin = 6m;
        config.OddsRangeMax = 10m;

        var candidates = new List<CandidateBet>
        {
            MakeCandidate("m1", "A", "B", 1.5m, 0.70m),
            MakeCandidate("m2", "C", "D", 1.5m, 0.70m),
        };

        var result = Optimizer.Build(candidates, config, Guid.NewGuid(), 100m);
        Assert.All(result.Slips, s => Assert.Equal(SlipStatus.Failed, s.Status));
    }

    private static StrategyConfig DefaultConfig() => new()
    {
        OddsRangeMin = 6m,
        OddsRangeMax = 10m,
        SlipsPerDay = 1,
        MinLegsPerSlip = 2,
        MaxLegsPerSlip = 6,
        MinEdgeThreshold = 0.02m,
        MinLegOdds = 1.20m,
        MaxLegOdds = 4.50m,
        OptimizerBeamWidth = 12,
        PreferDiversification = true
    };

    private static CandidateBet MakeCandidate(
        string fixtureId, string home, string away, decimal odds, decimal modelProb)
    {
        var match = new Match
        {
            ProviderFixtureId = fixtureId,
            League = "Test League",
            HomeTeam = home,
            AwayTeam = away,
            KickoffUtc = DateTimeOffset.UtcNow.AddHours(6)
        };

        return new CandidateBet
        {
            Match = match,
            Market = "1X2",
            Outcome = "Home",
            Odds = odds,
            ModelProbability = modelProb,
            PredictionSource = "DixonColes"
        };
    }
}
