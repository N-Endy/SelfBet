namespace SelfBet.Application.Models;

/// <summary>
/// SportyBet fixture with outcome IDs required for booking-code / order placement.
/// </summary>
public sealed class SportyBetPlacementFixture
{
    public string EventId { get; init; } = "";
    public string HomeTeam { get; set; } = "";
    public string AwayTeam { get; set; } = "";
    public DateTime? KickoffUtc { get; set; }
    public string HomeOutcomeId { get; set; } = "";
    public string DrawOutcomeId { get; set; } = "";
    public string AwayOutcomeId { get; set; } = "";
    public string Over25OutcomeId { get; set; } = "";
    public string Under25OutcomeId { get; set; } = "";
    public string BttsYesOutcomeId { get; set; } = "";
    public string BttsNoOutcomeId { get; set; } = "";
    public string Dc1XOutcomeId { get; set; } = "";
    public string DcX2OutcomeId { get; set; } = "";
    public string Dc12OutcomeId { get; set; } = "";
}

public sealed class SportyBetSelectionDto
{
    public string EventId { get; init; } = "";
    public string MarketId { get; init; } = "";
    public string OutcomeId { get; init; } = "";
    public string Specifier { get; init; } = "";
}

public sealed class SportyBetFixtureSnapshot
{
    public required IReadOnlyList<FixtureOddsDto> OddsFixtures { get; init; }
    public required IReadOnlyList<SportyBetPlacementFixture> PlacementFixtures { get; init; }
    public DateTimeOffset FetchedAtUtc { get; init; }
}
