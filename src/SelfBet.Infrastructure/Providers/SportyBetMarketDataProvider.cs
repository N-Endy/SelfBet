using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;

namespace SelfBet.Infrastructure.Providers;

/// <summary>
/// Pulls live fixtures + odds (1X2, O/U 2.5, BTTS) from SportyBet's public factsCenter API.
/// No authentication required. Results are cached 15 minutes to avoid hammering the endpoint.
/// </summary>
public sealed class SportyBetMarketDataProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<SportyBetMarketDataProvider> logger) : IFootballDataProvider
{
    private const string BaseUrl = "https://www.sportybet.com";
    private const string SoccerSportId = "sr:sport:1";
    private const string Market1X2 = "1";
    private const string MarketOverUnder = "18";
    private const string MarketBtts = "29";
    private const int PageSize = 100;
    private const int MaxPages = 15;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<IReadOnlyList<FixtureOddsDto>> GetUpcomingFixturesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        const string cacheKey = "sportybet_fixtures";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<FixtureOddsDto>? cached) && cached is not null)
        {
            logger.LogInformation("Returning {Count} fixtures from cache.", cached.Count);
            return cached;
        }

        var fixtures = await FetchFixturesAsync(fromUtc, toUtc, cancellationToken);
        if (fixtures.Count > 0)
            cache.Set(cacheKey, fixtures, CacheTtl);

        return fixtures;
    }

    private async Task<IReadOnlyList<FixtureOddsDto>> FetchFixturesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        var fixturesById = new Dictionary<string, FixtureBuilder>(StringComparer.Ordinal);
        var client = CreateClient();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (var page = 1; page <= MaxPages; page++)
        {
            var url = $"{BaseUrl}/api/ng/factsCenter/pcUpcomingEvents" +
                      $"?sportId={Uri.EscapeDataString(SoccerSportId)}" +
                      $"&marketId={Market1X2},{MarketOverUnder},{MarketBtts}" +
                      $"&pageSize={PageSize}&pageNum={page}" +
                      $"&todayGames=true&timeline=2.9&_t={ts}";

            logger.LogDebug("SportyBet GET page {Page}: {Url}", page, url);

            JsonDocument doc;
            try
            {
                var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("SportyBet page {Page} returned {Status}", page, response.StatusCode);
                    break;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                doc = JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch SportyBet page {Page}", page);
                break;
            }

            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("tournaments", out var tournaments))
            {
                logger.LogWarning("SportyBet page {Page}: unexpected response shape", page);
                break;
            }

            var tournamentCount = 0;
            foreach (var tournament in tournaments.EnumerateArray())
            {
                tournamentCount++;
                var leagueName = ExtractLeagueName(tournament);
                if (!tournament.TryGetProperty("events", out var events)) continue;

                foreach (var ev in events.EnumerateArray())
                {
                    try { ParseEvent(ev, leagueName, fromUtc, toUtc, fixturesById); }
                    catch (Exception ex) { logger.LogTrace(ex, "Skipping event parse error"); }
                }
            }

            logger.LogInformation("SportyBet page {Page}: {T} tournaments, {F} total fixtures",
                page, tournamentCount, fixturesById.Count);

            if (tournamentCount == 0) break;
        }

        var result = fixturesById.Values
            .Where(f => f.KickoffUtc.HasValue)
            .Select(f => f.ToDto())
            .ToList();

        logger.LogInformation("SportyBetMarketDataProvider: {Count} fixtures loaded.", result.Count);
        return result;
    }

    private static void ParseEvent(
        JsonElement ev,
        string league,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Dictionary<string, FixtureBuilder> map)
    {
        var eventId = ev.GetProperty("eventId").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(eventId)) return;

        var homeTeam = ev.TryGetProperty("homeTeamName", out var h) ? h.GetString() ?? "" : "";
        var awayTeam = ev.TryGetProperty("awayTeamName", out var a) ? a.GetString() ?? "" : "";

        DateTime? kickoffUtc = null;
        if (ev.TryGetProperty("estimateStartTime", out var est) && est.TryGetInt64(out var ms))
            kickoffUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

        if (kickoffUtc.HasValue)
        {
            var ko = new DateTimeOffset(kickoffUtc.Value, TimeSpan.Zero);
            if (ko < fromUtc || ko > toUtc) return;
        }

        if (!map.TryGetValue(eventId, out var builder))
        {
            builder = new FixtureBuilder
            {
                EventId = eventId,
                League = league,
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                KickoffUtc = kickoffUtc
            };
            map[eventId] = builder;
        }

        if (!ev.TryGetProperty("markets", out var markets)) return;

        foreach (var market in markets.EnumerateArray())
        {
            var marketId = market.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
            var specifier = market.TryGetProperty("specifier", out var spec) ? spec.GetString() ?? "" : "";
            if (!market.TryGetProperty("outcomes", out var outcomes)) continue;

            switch (marketId)
            {
                case Market1X2:
                    ParseOutcomes(outcomes, builder.Market1X2);
                    break;
                case MarketOverUnder when specifier == "total=2.5":
                    ParseOutcomes(outcomes, builder.MarketOver25);
                    break;
                case MarketBtts:
                    ParseOutcomes(outcomes, builder.MarketBtts);
                    break;
            }
        }
    }

    private static void ParseOutcomes(JsonElement outcomes, Dictionary<string, decimal> target)
    {
        foreach (var o in outcomes.EnumerateArray())
        {
            var id = o.TryGetProperty("id", out var oid) ? oid.GetString() ?? "" : "";
            var desc = o.TryGetProperty("desc", out var d) ? d.GetString() ?? "" : "";
            var key = NormalizeOutcomeKey(id, desc);
            var odds = TryParseDecimalOdds(o);
            if (odds > 1m) target[key] = odds;
        }
    }

    private static string NormalizeOutcomeKey(string id, string desc)
    {
        // 1X2: id=1→Home, id=2→Draw, id=3→Away
        // O/U: id=12→Over2.5, id=13→Under2.5
        // BTTS: id=74→Yes, id=76→No
        return id switch
        {
            "1" => "Home",
            "2" => "Draw",
            "3" => "Away",
            "12" => "Over2.5",
            "13" => "Under2.5",
            "74" => "Yes",
            "76" => "No",
            _ => string.IsNullOrWhiteSpace(desc) ? id : desc
        };
    }

    private static decimal TryParseDecimalOdds(JsonElement o)
    {
        foreach (var prop in new[] { "odds", "oddValue", "oddsValue", "price", "decimalOdds", "marketOdds" })
        {
            if (!o.TryGetProperty(prop, out var v)) continue;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && n > 1.0) return (decimal)n;
            if (v.ValueKind == JsonValueKind.String &&
                double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) &&
                s > 1.0) return (decimal)s;
        }
        return 0m;
    }

    private static string ExtractLeagueName(JsonElement tournament)
    {
        // tournament has sport.category.name + sport.category.tournament.name
        if (!tournament.TryGetProperty("sport", out var sport) ||
            !sport.TryGetProperty("category", out var category))
            return "";

        var country = category.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
        var comp = category.TryGetProperty("tournament", out var t) &&
                   t.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(country)) return comp;
        if (string.IsNullOrWhiteSpace(comp)) return country;
        return $"{country} - {comp}";
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("SportyBetData");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"{BaseUrl}/ng/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", BaseUrl);
        client.DefaultRequestHeaders.TryAddWithoutValidation("clientid", "web");
        client.DefaultRequestHeaders.TryAddWithoutValidation("platform", "web");
        client.DefaultRequestHeaders.TryAddWithoutValidation("operid", "2");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    // ── Internal fixture builder ────────────────────────────────────────────

    private sealed class FixtureBuilder
    {
        public string EventId { get; init; } = "";
        public string League { get; init; } = "";
        public string HomeTeam { get; init; } = "";
        public string AwayTeam { get; init; } = "";
        public DateTime? KickoffUtc { get; init; }
        public Dictionary<string, decimal> Market1X2 { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, decimal> MarketOver25 { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, decimal> MarketBtts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public FixtureOddsDto ToDto()
        {
            var markets = new List<MarketOddsDto>();

            if (Market1X2.Count > 0)
                markets.Add(new MarketOddsDto
                {
                    Market = "1X2",
                    Outcomes = Market1X2.Select(kv => new OutcomeOddsDto { Outcome = kv.Key, Odds = kv.Value }).ToList()
                });

            if (MarketOver25.Count > 0)
                markets.Add(new MarketOddsDto
                {
                    Market = "Over2.5",
                    Outcomes = MarketOver25
                        .Where(kv => kv.Key == "Over2.5")
                        .Select(kv => new OutcomeOddsDto { Outcome = kv.Key, Odds = kv.Value })
                        .ToList()
                });

            // Also expose Under2.5 as its own market
            var under25 = MarketOver25.TryGetValue("Under2.5", out var u25odds) ? u25odds : 0m;
            if (under25 > 1m)
                markets.Add(new MarketOddsDto
                {
                    Market = "Under2.5",
                    Outcomes = [new OutcomeOddsDto { Outcome = "Under2.5", Odds = under25 }]
                });

            if (MarketBtts.TryGetValue("Yes", out var bttsYes) && bttsYes > 1m)
                markets.Add(new MarketOddsDto
                {
                    Market = "BTTS",
                    Outcomes = [new OutcomeOddsDto { Outcome = "Yes", Odds = bttsYes }]
                });

            // Over1.5 is not in SportyBet's default page; skip. Add DoubleChance derived from 1X2 if all three legs present.
            if (Market1X2.TryGetValue("Home", out var homeOdds) &&
                Market1X2.TryGetValue("Draw", out var drawOdds) &&
                Market1X2.TryGetValue("Away", out var awayOdds))
            {
            // DoubleChance: harmonic of adjacent 1X2 outcomes
            var hd = HarmonicMean(homeOdds, drawOdds);
            var da = HarmonicMean(drawOdds, awayOdds);
            var dcOutcomes = new List<OutcomeOddsDto>();
            if (hd > 1m) dcOutcomes.Add(new OutcomeOddsDto { Outcome = "HomeOrDraw", Odds = hd });
            if (da > 1m) dcOutcomes.Add(new OutcomeOddsDto { Outcome = "DrawOrAway", Odds = da });
            if (dcOutcomes.Count > 0)
                markets.Add(new MarketOddsDto { Market = "DoubleChance", Outcomes = dcOutcomes });

            // DrawNoBet: remove draw from 1X2, re-normalise
            var impliedHome = homeOdds > 0 ? 1m / homeOdds : 0m;
            var impliedAway = awayOdds > 0 ? 1m / awayOdds : 0m;
            var sum = impliedHome + impliedAway;
            if (sum > 0)
            {
                var dnbHome = Math.Round(sum / impliedHome, 3);
                var dnbAway = Math.Round(sum / impliedAway, 3);
                var dnbOutcomes = new List<OutcomeOddsDto>();
                if (dnbHome > 1m) dnbOutcomes.Add(new OutcomeOddsDto { Outcome = "Home", Odds = dnbHome });
                if (dnbAway > 1m) dnbOutcomes.Add(new OutcomeOddsDto { Outcome = "Away", Odds = dnbAway });
                if (dnbOutcomes.Count > 0)
                    markets.Add(new MarketOddsDto { Market = "DrawNoBet", Outcomes = dnbOutcomes });
            }
            }

            return new FixtureOddsDto
            {
                FixtureId = EventId,
                League = League,
                HomeTeam = HomeTeam,
                AwayTeam = AwayTeam,
                KickoffUtc = KickoffUtc.HasValue
                    ? new DateTimeOffset(KickoffUtc.Value, TimeSpan.Zero)
                    : DateTimeOffset.UtcNow.AddHours(3),
                Markets = markets
            };
        }

        private static decimal HarmonicMean(decimal a, decimal b)
        {
            if (a <= 1m || b <= 1m) return 0m;
            var p1 = 1m / a;
            var p2 = 1m / b;
            var combined = p1 + p2;
            return combined > 0 ? Math.Round(1m / combined, 3) : 0m;
        }
    }
}
