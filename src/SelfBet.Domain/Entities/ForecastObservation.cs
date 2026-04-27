namespace SelfBet.Domain.Entities;

/// <summary>
/// One resolved bet outcome used for probability calibration and threshold tuning.
/// Written when a slip leg is settled (won/lost).
/// </summary>
public sealed class ForecastObservation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SlipLegId { get; init; }
    public string Market { get; init; } = "";
    public string Outcome { get; init; } = "";
    public string League { get; init; } = "";
    public decimal ModelProbability { get; init; }
    public decimal BookOdds { get; init; }
    public bool Correct { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAtUtc { get; init; }
}
