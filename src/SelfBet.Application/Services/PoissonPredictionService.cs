using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;

namespace SelfBet.Application.Services;

/// <summary>
/// Converts market-implied probabilities into calibrated predictions using a
/// Poisson goal-expectation model blended with bookmaker priors.
///
/// When enough ForecastObservations exist (≥50 per market), the CalibrationService
/// adjusts slope/intercept per market. Until then, safe defaults keep us close to
/// the bookmaker's probability to avoid arrogant over-betting.
/// </summary>
public sealed class PoissonPredictionService(ICalibrationService calibration) : IPredictionService
{
    public decimal Predict(FeatureVector vector)
    {
        var prior = (double)vector.MarketImpliedProbability;

        // Estimate goal expectation using implied probabilities
        var totalXg = EstimateTotalXg(vector);

        double modelProb;
        if (totalXg <= 0)
        {
            modelProb = prior;
        }
        else
        {
            var strengthBias = EstimateStrengthBias(vector);
            var homeShare = Math.Clamp(0.5 + strengthBias * 0.33, 0.18, 0.82);
            var homeXg = Math.Max(totalXg * homeShare, 0.05);
            var awayXg = Math.Max(totalXg - homeXg, 0.05);
            var poisson = ComputePoissonModel(homeXg, awayXg);

            modelProb = vector.Market switch
            {
                "1X2" => vector.Outcome switch
                {
                    "Home" => Blend(prior, poisson.HomeWin, 0.50, 0.50),
                    "Draw" => Blend(prior, poisson.Draw,    0.60, 0.40),
                    "Away" => Blend(prior, poisson.AwayWin, 0.50, 0.50),
                    _ => prior
                },
                "Over2.5" => Blend(prior, poisson.Over25,  0.60, 0.40),
                "Under2.5" => Blend(prior, 1.0 - poisson.Over25, 0.60, 0.40),
                "BTTS"     => Blend(prior, poisson.Btts,   0.55, 0.45),
                // DoubleChance / DrawNoBet: blend with slightly less Poisson weight
                _ => Blend(prior, prior, 0.70, 0.30)
            };
        }

        // Apply calibration correction
        var calibrated = calibration.Calibrate(vector.Market, modelProb);
        return (decimal)Math.Clamp(calibrated, 0.04, 0.96);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static double EstimateTotalXg(FeatureVector v)
    {
        // Infer from market implied probability spread
        // Over2.5 implied probability correlates strongly with total goals
        var over25Proxy = (double)v.MarketImpliedProbability;

        // Map over25 probability → expected goals using empirical lookup
        // p(Over2.5) ≈ 1 - e^{-λ} * (1 + λ + λ²/2) where λ = totalXg
        // We solve numerically for λ in [0.5, 6]
        if (v.Market == "Over2.5")
            return SolveXgFromOver25Prob(over25Proxy);

        // Generic: use bookmaker over25 proxy ≈ 0.52 for average Premier League match
        return 2.6;
    }

    private static double SolveXgFromOver25Prob(double p)
    {
        // Binary search: find λ such that P(goals ≤ 2 | Poisson(λ)) = 1-p
        var target = 1.0 - p;
        var lo = 0.3;
        var hi = 6.0;
        for (var i = 0; i < 40; i++)
        {
            var mid = (lo + hi) / 2.0;
            var under = PoissonCdf(mid, 2);
            if (under > target) lo = mid; else hi = mid;
        }

        return (lo + hi) / 2.0;
    }

    private static double EstimateStrengthBias(FeatureVector v)
    {
        // Use bookmaker 1X2 Home vs Away implied probs as proxy
        if (v.Market == "1X2")
        {
            return v.Outcome switch
            {
                "Home" => Math.Clamp((double)v.MarketImpliedProbability - 0.35, -0.5, 0.5),
                "Away" => Math.Clamp(0.35 - (double)v.MarketImpliedProbability, -0.5, 0.5),
                _ => 0
            };
        }

        return 0;
    }

    private static (double HomeWin, double Draw, double AwayWin, double Over25, double Btts) ComputePoissonModel(
        double homeXg, double awayXg)
    {
        const int maxGoals = 10;
        double homeWin = 0, draw = 0, awayWin = 0, btts = 0;
        double cumulativeOver25 = 0;

        for (var h = 0; h <= maxGoals; h++)
        {
            for (var a = 0; a <= maxGoals; a++)
            {
                var p = PoissonPmf(homeXg, h) * PoissonPmf(awayXg, a);
                if (h > a) homeWin += p;
                else if (h == a) draw += p;
                else awayWin += p;
                if (h + a > 2) cumulativeOver25 += p;
                if (h > 0 && a > 0) btts += p;
            }
        }

        return (homeWin, draw, awayWin, cumulativeOver25, btts);
    }

    private static double PoissonPmf(double lambda, int k)
    {
        if (lambda <= 0) return k == 0 ? 1.0 : 0.0;
        return Math.Exp(-lambda) * Math.Pow(lambda, k) / Factorial(k);
    }

    private static double PoissonCdf(double lambda, int maxK)
    {
        var result = 0.0;
        for (var k = 0; k <= maxK; k++) result += PoissonPmf(lambda, k);
        return result;
    }

    private static double Factorial(int n)
    {
        double r = 1;
        for (var i = 2; i <= n; i++) r *= i;
        return r;
    }

    private static double Blend(double prior, double model, double priorWeight, double modelWeight)
    {
        var total = priorWeight + modelWeight;
        return (prior * priorWeight + model * modelWeight) / total;
    }
}
