namespace SelfBet.Domain.Entities;

/// <summary>
/// Per-market calibration scaling factor (intercept + slope applied to model probability).
/// Rebuilt from ForecastObservations via CalibrationService.
/// </summary>
public sealed class CalibrationProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Market { get; init; } = "";
    /// <summary>Isotonic/Platt scaling: calibrated_p = Intercept + Slope * model_p</summary>
    public double Slope { get; set; } = 1.0;
    public double Intercept { get; set; } = 0.0;
    /// <summary>Minimum publish threshold for this market (model probability must exceed this).</summary>
    public double PublishThreshold { get; set; } = 0.55;
    public int SampleCount { get; set; } = 0;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
