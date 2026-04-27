namespace SelfBet.Infrastructure.Providers;

public sealed class ApiFootballOptions
{
    /// <summary>
    /// API-Football v3 API key (https://www.api-football.com/). When empty the
    /// historical-match provider is disabled and predictions fall back to the
    /// bookmaker-derived heuristic.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://v3.football.api-sports.io";

    /// <summary>
    /// How many past seasons to fetch per league when (re)building the historical
    /// dataset. 2 is a sensible default — gives ~600+ matches per top league.
    /// </summary>
    public int SeasonsToFetch { get; set; } = 2;

    /// <summary>
    /// Map of SportyBet league display name → API-Football league id.
    /// Populated from configuration so the user can extend it without code changes.
    /// </summary>
    public Dictionary<string, int> LeagueIdMap { get; set; } = new();
}
