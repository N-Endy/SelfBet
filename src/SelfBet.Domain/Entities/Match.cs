namespace SelfBet.Domain.Entities;

public sealed class Match
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string ProviderFixtureId { get; init; }
    public required string League { get; init; }
    public required string HomeTeam { get; init; }
    public required string AwayTeam { get; init; }
    public DateTimeOffset KickoffUtc { get; init; }

    public string Title => $"{HomeTeam} vs {AwayTeam}";
}
