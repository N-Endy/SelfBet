namespace SelfBet.Domain.Entities;

public sealed class Prediction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid MatchId { get; init; }
    public required string Market { get; init; }
    public required string Outcome { get; init; }
    public decimal Probability { get; init; }
    public string ModelVersion { get; init; } = "v1.0";
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
