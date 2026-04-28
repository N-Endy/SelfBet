namespace SelfBet.Domain.Entities;

public sealed class SlipLeg
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SlipId { get; init; }
    public Guid MatchId { get; init; }
    public required string MatchTitle { get; init; }
    public required string League { get; init; }
    public required string Market { get; init; }
    public required string Outcome { get; init; }
    public decimal Odds { get; init; }
    public decimal ModelProbability { get; init; }
    public DateTimeOffset KickoffUtc { get; init; }
}
