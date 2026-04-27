namespace SelfBet.Domain.Entities;

public sealed class HistoricalMatch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string ProviderFixtureId { get; init; }
    public required string League { get; init; }
    public required string Season { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public DateTimeOffset KickoffUtc { get; init; }
    public int HomeGoals { get; init; }
    public int AwayGoals { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
