using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;

namespace SelfBet.Application.Services;

/// <summary>
/// Match-outcome probability service that combines:
///   1. A Dixon-Coles team-strength model (when <see cref="FeatureVector.FixtureExpectation"/>
///      has been pre-computed by the run engine).
///   2. The bookmaker's implied probability as a prior.
///   3. A market-specific Platt-scaling calibration learned from settled bets.
///
/// When team strengths are available, the model computes its own per-fixture
/// goal expectations (λ_home, λ_away) and weights itself heavily against the
/// bookmaker prior. When the league has no fitted data yet, it falls back to
/// the bookmaker-derived heuristic so the system still functions safely
/// out-of-the-box.
/// </summary>
public sealed class PoissonPredictionService(ICalibrationService calibration) : IPredictionService
{
    public decimal Predict(FeatureVector vector)
    {
        var prior = (double)vector.MarketImpliedProbability;
        double modelProb;
        double modelWeight;

        var expectation = vector.FixtureExpectation;
        if (expectation is not null)
        {
            var grid = ComputePoissonGrid(expectation.LambdaHome, expectation.LambdaAway, expectation.DixonColesRho);
            modelProb = ProbabilityFor(grid, vector.Market, vector.Outcome);
            modelWeight = SampleWeight(expectation.HomeSampleSize, expectation.AwaySampleSize);
        }
        else
        {
            var fallback = FallbackBookmakerModel(vector);
            modelProb = fallback ?? prior;
            modelWeight = fallback is null ? 0.0 : 0.30;
        }

        var blended = Blend(prior, modelProb, 1.0 - modelWeight, modelWeight);
        var calibrated = calibration.Calibrate(vector.Market, blended);
        return (decimal)Math.Clamp(calibrated, 0.04, 0.96);
    }

    private static double SampleWeight(int homeSize, int awaySize)
    {
        var smallest = Math.Min(homeSize, awaySize);
        // 6 matches → 0.35, 20 → 0.55, 50 → 0.70, 100+ → 0.75
        if (smallest <= 6) return 0.35;
        if (smallest >= 100) return 0.75;
        return 0.35 + (smallest - 6) / 94.0 * 0.40;
    }

    private static double? FallbackBookmakerModel(FeatureVector v)
    {
        // Original heuristic kept verbatim for graceful degradation.
        var prior = (double)v.MarketImpliedProbability;
        var totalXg = v.Market == "Over2.5" ? SolveXgFromOver25Prob(prior) : 2.6;
        if (totalXg <= 0) return null;

        var bias = v.Market == "1X2"
            ? v.Outcome switch
            {
                "Home" => Math.Clamp(prior - 0.35, -0.5, 0.5),
                "Away" => Math.Clamp(0.35 - prior, -0.5, 0.5),
                _ => 0
            }
            : 0;
        var homeShare = Math.Clamp(0.5 + bias * 0.33, 0.18, 0.82);
        var lambdaHome = Math.Max(totalXg * homeShare, 0.05);
        var lambdaAway = Math.Max(totalXg - lambdaHome, 0.05);
        var grid = ComputePoissonGrid(lambdaHome, lambdaAway, 0.0);
        return ProbabilityFor(grid, v.Market, v.Outcome);
    }

    private static double ProbabilityFor(PoissonGrid g, string market, string outcome)
    {
        return market switch
        {
            "1X2" => outcome switch
            {
                "Home" => g.HomeWin,
                "Draw" => g.Draw,
                "Away" => g.AwayWin,
                _ => 0
            },
            "Over2.5"  => g.Over25,
            "Under2.5" => 1.0 - g.Over25,
            "BTTS"     => g.Btts,
            "DoubleChance" => outcome switch
            {
                "1X" or "HOMEORDRAW" or "HOMEDRAW" => g.HomeWin + g.Draw,
                "X2" or "DRAWORAWAY" or "DRAWAWAY" => g.AwayWin + g.Draw,
                "12" or "HOMEORAWAY" or "HOMEAWAY" => g.HomeWin + g.AwayWin,
                _ => 0
            },
            "DrawNoBet" => outcome switch
            {
                "Home" => g.HomeWin / Math.Max(g.HomeWin + g.AwayWin, 1e-9),
                "Away" => g.AwayWin / Math.Max(g.HomeWin + g.AwayWin, 1e-9),
                _ => 0
            },
            _ => 0
        };
    }

    private static PoissonGrid ComputePoissonGrid(double lambdaHome, double lambdaAway, double rho)
    {
        const int maxGoals = 10;
        double homeWin = 0, draw = 0, awayWin = 0, btts = 0, over25 = 0;
        var pmfH = PrecomputePmf(lambdaHome, maxGoals);
        var pmfA = PrecomputePmf(lambdaAway, maxGoals);

        for (var h = 0; h <= maxGoals; h++)
        {
            for (var a = 0; a <= maxGoals; a++)
            {
                var p = pmfH[h] * pmfA[a] * Tau(h, a, lambdaHome, lambdaAway, rho);
                if (h > a) homeWin += p;
                else if (h == a) draw += p;
                else awayWin += p;
                if (h + a > 2) over25 += p;
                if (h > 0 && a > 0) btts += p;
            }
        }

        // Renormalise after Dixon-Coles τ correction
        var total = homeWin + draw + awayWin;
        if (total > 0 && Math.Abs(total - 1.0) > 1e-3)
        {
            homeWin /= total;
            draw    /= total;
            awayWin /= total;
        }

        return new PoissonGrid(homeWin, draw, awayWin, over25, btts);
    }

    private static double Tau(int h, int a, double lH, double lA, double rho)
    {
        return (h, a) switch
        {
            (0, 0) => Math.Max(1.0 - lH * lA * rho, 1e-9),
            (0, 1) => 1.0 + lH * rho,
            (1, 0) => 1.0 + lA * rho,
            (1, 1) => 1.0 - rho,
            _ => 1.0
        };
    }

    private static double[] PrecomputePmf(double lambda, int maxK)
    {
        var arr = new double[maxK + 1];
        var expL = Math.Exp(-lambda);
        var lk = 1.0;
        var k_fact = 1.0;
        arr[0] = expL;
        for (var k = 1; k <= maxK; k++)
        {
            lk *= lambda;
            k_fact *= k;
            arr[k] = expL * lk / k_fact;
        }
        return arr;
    }

    private static double SolveXgFromOver25Prob(double p)
    {
        var target = 1.0 - p;
        double lo = 0.3, hi = 6.0;
        for (var i = 0; i < 40; i++)
        {
            var mid = (lo + hi) / 2.0;
            var under = PoissonCdf(mid, 2);
            if (under > target) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2.0;
    }

    private static double PoissonCdf(double lambda, int maxK)
    {
        var pmf = PrecomputePmf(lambda, maxK);
        var sum = 0.0;
        for (var k = 0; k <= maxK; k++) sum += pmf[k];
        return sum;
    }

    private static double Blend(double prior, double model, double priorW, double modelW)
    {
        var total = priorW + modelW;
        if (total <= 0) return prior;
        return (prior * priorW + model * modelW) / total;
    }

    private readonly record struct PoissonGrid(
        double HomeWin, double Draw, double AwayWin, double Over25, double Btts);
}
