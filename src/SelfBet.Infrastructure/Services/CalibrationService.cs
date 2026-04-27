using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;
using SelfBet.Infrastructure.Persistence;

namespace SelfBet.Infrastructure.Services;

/// <summary>
/// Platt-scaling calibration service.
/// Reads ForecastObservations, fits slope+intercept per market,
/// writes CalibrationProfile rows, and applies them at prediction time.
///
/// Minimum 50 observations required before calibration kicks in.
/// Until then acts as identity transform (slope=1, intercept=0).
/// </summary>
public sealed class CalibrationService(SelfBetDbContext db, ILogger<CalibrationService> logger)
    : ICalibrationService
{
    private const int MinSamples = 50;

    // In-memory cache of profiles, refreshed after each rebuild
    private Dictionary<string, CalibrationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public double Calibrate(string market, double rawProbability)
    {
        if (_profiles.TryGetValue(market, out var profile) && profile.SampleCount >= MinSamples)
        {
            var calibrated = profile.Intercept + profile.Slope * rawProbability;
            return Math.Clamp(calibrated, 0.04, 0.96);
        }

        return rawProbability;
    }

    public async Task RebuildAsync(CancellationToken ct = default)
    {
        var observations = await db.ForecastObservations
            .Where(o => o.ResolvedAtUtc != null)
            .ToListAsync(ct);

        if (observations.Count == 0)
        {
            logger.LogInformation("CalibrationService: no resolved observations yet, skipping rebuild.");
            return;
        }

        var byMarket = observations.GroupBy(o => o.Market, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byMarket)
        {
            var market = group.Key;
            var samples = group.ToList();
            if (samples.Count < MinSamples)
            {
                logger.LogInformation("CalibrationService: market {Market} has {N} samples (< {Min}), skipping.",
                    market, samples.Count, MinSamples);
                continue;
            }

            var (slope, intercept) = FitPlattScaling(samples);
            var publishThreshold = EstimatePublishThreshold(samples);

            var existing = await db.CalibrationProfiles
                .FirstOrDefaultAsync(p => p.Market == market, ct);

            if (existing is null)
            {
                db.CalibrationProfiles.Add(new CalibrationProfile
                {
                    Market = market,
                    Slope = slope,
                    Intercept = intercept,
                    PublishThreshold = publishThreshold,
                    SampleCount = samples.Count,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.Slope = slope;
                existing.Intercept = intercept;
                existing.PublishThreshold = publishThreshold;
                existing.SampleCount = samples.Count;
                existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            logger.LogInformation(
                "CalibrationService: {Market} — slope={Slope:F4} intercept={Intercept:F4} threshold={T:F3} n={N}",
                market, slope, intercept, publishThreshold, samples.Count);
        }

        await db.SaveChangesAsync(ct);

        // Reload in-memory profiles
        _profiles = (await db.CalibrationProfiles.ToListAsync(ct))
            .ToDictionary(p => p.Market, StringComparer.OrdinalIgnoreCase);
    }

    private static (double Slope, double Intercept) FitPlattScaling(List<ForecastObservation> samples)
    {
        // Ordinary least squares: y = a + b*x where y=actual(0/1), x=model_prob
        var n = (double)samples.Count;
        var sumX = samples.Sum(s => (double)s.ModelProbability);
        var sumY = samples.Sum(s => s.Correct ? 1.0 : 0.0);
        var sumXX = samples.Sum(s => (double)(s.ModelProbability * s.ModelProbability));
        var sumXY = samples.Sum(s => s.Correct ? (double)s.ModelProbability : 0.0);

        var denom = n * sumXX - sumX * sumX;
        if (Math.Abs(denom) < 1e-10) return (1.0, 0.0);

        var slope = (n * sumXY - sumX * sumY) / denom;
        var intercept = (sumY - slope * sumX) / n;

        // Constrain to reasonable range to avoid wild extrapolation
        slope = Math.Clamp(slope, 0.3, 2.5);
        intercept = Math.Clamp(intercept, -0.2, 0.2);

        return (slope, intercept);
    }

    private static double EstimatePublishThreshold(List<ForecastObservation> samples)
    {
        // Find minimum model_probability threshold that yields ≥52% accuracy
        var sorted = samples.OrderBy(s => s.ModelProbability).ToList();
        for (var thresh = 0.50; thresh <= 0.80; thresh += 0.01)
        {
            var above = sorted.Where(s => (double)s.ModelProbability >= thresh).ToList();
            if (above.Count < 10) break;
            var accuracy = above.Count(s => s.Correct) / (double)above.Count;
            if (accuracy >= 0.52) return thresh;
        }

        return 0.55;
    }
}
