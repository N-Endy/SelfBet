using SelfBet.Domain.ValueObjects;

namespace SelfBet.Domain.Entities;

public sealed class StrategyConfig
{
    // Singleton row — always Id = 1
    public int Id { get; set; } = 1;

    public decimal OddsRangeMin { get; set; } = 6m;
    public decimal OddsRangeMax { get; set; } = 10m;

    [System.Text.Json.Serialization.JsonIgnore]
    public OddsRange OddsRange
    {
        get => new(OddsRangeMin, OddsRangeMax);
        set { OddsRangeMin = value.Min; OddsRangeMax = value.Max; }
    }

    public decimal StakePercentagePerSlip { get; set; } = 0.02m;
    public int SlipsPerDay { get; set; } = 2;
    public int MaxLegsPerSlip { get; set; } = 6;
    public int MinLegsPerSlip { get; set; } = 2;
    public decimal MinEdgeThreshold { get; set; } = 0.02m;
    public decimal MinLegOdds { get; set; } = 1.20m;
    public decimal MaxLegOdds { get; set; } = 4.50m;
    public decimal StakeIncrement { get; set; } = 50m;
    public bool AutomationEnabled { get; set; } = false;
    public bool RequireConfirmationOnRisk { get; set; } = true;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Stored as comma-separated strings in Postgres
    public string EnabledLeaguesCsv { get; set; } = string.Join(",", DefaultLeagues());
    public string AllowedMarketsCsv { get; set; } = string.Join(",", DefaultMarkets());

    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> EnabledLeagues
    {
        get => EnabledLeaguesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        set => EnabledLeaguesCsv = string.Join(",", value);
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public List<string> AllowedMarkets
    {
        get => AllowedMarketsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        set => AllowedMarketsCsv = string.Join(",", value);
    }

    public static IEnumerable<string> DefaultLeagues() =>
    [
        "England - Premier League",
        "England - Championship",
        "Spain - LaLiga",
        "Germany - Bundesliga",
        "Italy - Serie A",
        "France - Ligue 1",
        "Netherlands - Eredivisie",
        "Portugal - Primeira Liga",
        "Belgium - Pro League",
        "Turkey - Super Lig",
        "UEFA - Champions League",
        "UEFA - Europa League",
        "UEFA - Conference League",
        "USA - MLS",
        "Mexico - Liga MX",
        "Brazil - Serie A",
        "Saudi Arabia - Pro League"
    ];

    public static IEnumerable<string> DefaultMarkets() =>
    [
        "1X2",
        "Over2.5",
        "Under2.5",
        "BTTS",
        "DoubleChance",
        "DrawNoBet",
        "Over1.5"
    ];
}
