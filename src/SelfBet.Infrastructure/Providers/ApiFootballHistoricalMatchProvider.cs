using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Providers;

/// <summary>
/// Pulls finished matches per league/season from API-Football v3.
/// Endpoint: <c>GET /fixtures?league={id}&amp;season={year}&amp;status=FT</c>.
/// </summary>
public sealed class ApiFootballHistoricalMatchProvider(
    HttpClient httpClient,
    IOptions<ApiFootballOptions> options,
    ILogger<ApiFootballHistoricalMatchProvider> logger)
    : IHistoricalMatchProvider
{
    private readonly ApiFootballOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<HistoricalMatch>> FetchAsync(
        string league,
        IReadOnlyList<string> seasons,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("API-Football not configured; skipping fetch for league {League}", league);
            return [];
        }

        if (!_options.LeagueIdMap.TryGetValue(league, out var leagueId))
        {
            logger.LogWarning("No API-Football league id mapped for '{League}'. Configure ApiFootball:LeagueIdMap.", league);
            return [];
        }

        var collected = new List<HistoricalMatch>();
        foreach (var season in seasons)
        {
            try
            {
                var path = $"/fixtures?league={leagueId}&season={season}&status=FT";
                using var req = new HttpRequestMessage(HttpMethod.Get, path);
                req.Headers.Add("x-apisports-key", _options.ApiKey);

                using var res = await httpClient.SendAsync(req, cancellationToken);
                if (!res.IsSuccessStatusCode)
                {
                    logger.LogWarning("API-Football returned {Status} for league {League} season {Season}",
                        res.StatusCode, league, season);
                    continue;
                }

                var payload = await res.Content.ReadFromJsonAsync<ApiFootballFixturesResponse>(cancellationToken: cancellationToken);
                if (payload?.Response is null) continue;

                foreach (var item in payload.Response)
                {
                    var fx = item.Fixture;
                    var t = item.Teams;
                    var g = item.Goals;
                    if (fx is null || t?.Home is null || t.Away is null || g is null) continue;
                    if (g.Home is null || g.Away is null) continue;

                    collected.Add(new HistoricalMatch
                    {
                        ProviderFixtureId = fx.Id.ToString(),
                        League = league,
                        Season = season,
                        HomeTeam = t.Home.Name ?? "?",
                        AwayTeam = t.Away.Name ?? "?",
                        KickoffUtc = fx.Date,
                        HomeGoals = g.Home.Value,
                        AwayGoals = g.Away.Value
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch API-Football fixtures for league {League} season {Season}",
                    league, season);
            }
        }

        logger.LogInformation("API-Football: fetched {Count} historical matches for league {League}",
            collected.Count, league);
        return collected;
    }

    // ── DTOs (subset of API-Football v3 schema) ──────────────────────────────
    private sealed class ApiFootballFixturesResponse
    {
        [JsonPropertyName("response")] public List<ApiFootballFixtureItem>? Response { get; set; }
    }

    private sealed class ApiFootballFixtureItem
    {
        [JsonPropertyName("fixture")] public ApiFootballFixture? Fixture { get; set; }
        [JsonPropertyName("teams")]   public ApiFootballTeams?   Teams   { get; set; }
        [JsonPropertyName("goals")]   public ApiFootballGoals?   Goals   { get; set; }
    }

    private sealed class ApiFootballFixture
    {
        [JsonPropertyName("id")]   public long Id { get; set; }
        [JsonPropertyName("date")] public DateTimeOffset Date { get; set; }
    }

    private sealed class ApiFootballTeams
    {
        [JsonPropertyName("home")] public ApiFootballTeam? Home { get; set; }
        [JsonPropertyName("away")] public ApiFootballTeam? Away { get; set; }
    }

    private sealed class ApiFootballTeam
    {
        [JsonPropertyName("id")]   public long Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class ApiFootballGoals
    {
        [JsonPropertyName("home")] public int? Home { get; set; }
        [JsonPropertyName("away")] public int? Away { get; set; }
    }
}
