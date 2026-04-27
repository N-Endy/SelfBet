namespace SelfBet.Application.Abstractions;

/// <summary>
/// Applies per-market calibration scaling to a raw model probability.
/// Falls back to identity transform until enough observations are accumulated.
/// </summary>
public interface ICalibrationService
{
    double Calibrate(string market, double rawProbability);
    Task RebuildAsync(CancellationToken ct = default);
}
