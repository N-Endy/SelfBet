using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;

namespace SelfBet.Application.Services;

public sealed class FeatureBuilder : IFeatureBuilder
{
    public IReadOnlyList<FeatureVector> Build(IReadOnlyList<FixtureOddsDto> fixtures)
    {
        var vectors = new List<FeatureVector>();

        foreach (var fixture in fixtures)
        {
            var home = fixture.HomeStats;
            var away = fixture.AwayStats;
            var attackDelta = (home?.RollingXg ?? 1.3m) - (away?.RollingXgAgainst ?? 1.3m);
            var formDelta = (home?.Form ?? 0m) - (away?.Form ?? 0m);
            var restDelta = (home?.RestDays ?? 5) - (away?.RestDays ?? 5);
            var injuryPenalty = (home?.InjuriesKey ?? 0) + (away?.InjuriesKey ?? 0);

            foreach (var market in fixture.Markets)
            {
                var dispersion = ComputeDispersion(market);
                foreach (var outcome in market.Outcomes)
                {
                    if (outcome.Odds <= 1m)
                    {
                        continue;
                    }

                    vectors.Add(new FeatureVector
                    {
                        FixtureId = fixture.FixtureId,
                        Market = market.Market,
                        Outcome = outcome.Outcome,
                        MarketImpliedProbability = Math.Round(1m / outcome.Odds, 4),
                        AttackStrengthDelta = Math.Round(attackDelta, 3),
                        FormDelta = Math.Round(formDelta, 3),
                        RestDaysDelta = restDelta,
                        InjuriesPenalty = injuryPenalty,
                        MarketDispersion = dispersion
                    });
                }
            }
        }

        return vectors;
    }

    private static decimal ComputeDispersion(MarketOddsDto market)
    {
        if (market.Outcomes.Count < 2)
        {
            return 0m;
        }

        var implieds = market.Outcomes
            .Where(o => o.Odds > 1m)
            .Select(o => 1m / o.Odds)
            .ToList();

        if (implieds.Count < 2)
        {
            return 0m;
        }

        var mean = implieds.Average();
        var variance = implieds.Sum(p => (p - mean) * (p - mean)) / implieds.Count;
        return Math.Round(variance, 4);
    }
}
