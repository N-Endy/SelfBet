using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Application.Services;

namespace SelfBet.Infrastructure.Providers;

public sealed class SportyBetFixtureCache(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<SportyBetFixtureCache> logger) : ISportyBetFixtureCache
{
    private const string BaseUrl = "https://www.sportybet.com";
    private const string CacheKey = "sportybet_fixture_snapshot";
    private const string SoccerSportId = "sr:sport:1";
    private const string Market1X2 = "1";
    private const string MarketOverUnder = "18";
    private const string MarketBtts = "29";
    private const string MarketDoubleChance = "10";
    private const int PageSize = 100;
    private const int MaxPages = 15;
    private const int MaxParallelPages = 4;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<SportyBetFixtureSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out SportyBetFixtureSnapshot? cached) && cached is not null)
        {
            logger.LogDebug("Returning SportyBet snapshot from cache ({Count} fixtures).", cached.OddsFixtures.Count);
            return cached;
        }

        var snapshot = await FetchSnapshotAsync(cancellationToken);
        if (snapshot.OddsFixtures.Count > 0)
            cache.Set(CacheKey, snapshot, CacheTtl);

        return snapshot;
    }

    private async Task<SportyBetFixtureSnapshot> FetchSnapshotAsync(CancellationToken ct)
    {
        var fixturesById = new Dictionary<string, FixtureBuilder>(StringComparer.Ordinal);
        var client = CreateClient();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var pages = Enumerable.Range(1, MaxPages).ToList();
        using var gate = new SemaphoreSlim(MaxParallelPages);

        var mapLock = new object();
        var tasks = pages.Select(async page =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await FetchAndParsePageAsync(client, page, ts, fixturesById, mapLock, ct);
            }
            finally
            {
                gate.Release();
            }
        });

        var pageResults = await Task.WhenAll(tasks);
        var emptyStreak = 0;

        foreach (var (page, tournamentCount) in pageResults.OrderBy(r => r.Page))
        {
            if (tournamentCount == 0)
            {
                emptyStreak++;
                if (emptyStreak >= 2) break;
                continue;
            }

            emptyStreak = 0;
        }

        var oddsFixtures = fixturesById.Values
            .Where(f => f.KickoffUtc.HasValue)
            .Select(f => f.ToDto())
            .ToList();

        var placementFixtures = fixturesById.Values
            .Select(f => f.ToPlacementFixture())
            .ToList();

        logger.LogInformation(
            "SportyBetFixtureCache: loaded {Count} fixtures ({Placement} placement-ready).",
            oddsFixtures.Count, placementFixtures.Count);

        return new SportyBetFixtureSnapshot
        {
            OddsFixtures = oddsFixtures,
            PlacementFixtures = placementFixtures,
            FetchedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task<(int Page, int TournamentCount)> FetchAndParsePageAsync(
        HttpClient client,
        int page,
        long ts,
        Dictionary<string, FixtureBuilder> fixturesById,
        object mapLock,
        CancellationToken ct)
    {
        var url = $"{BaseUrl}/api/ng/factsCenter/pcUpcomingEvents" +
                  $"?sportId={Uri.EscapeDataString(SoccerSportId)}" +
                  $"&marketId={Market1X2},{MarketOverUnder},{MarketBtts},{MarketDoubleChance}" +
                  $"&pageSize={PageSize}&pageNum={page}" +
                  $"&todayGames=true&timeline=2.9&_t={ts}";

        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return (page, 0);

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("tournaments", out var tournaments))
                return (page, 0);

            var tournamentCount = 0;
            lock (mapLock)
            {
                foreach (var tournament in tournaments.EnumerateArray())
                {
                    tournamentCount++;
                    var leagueName = ExtractLeagueName(tournament);
                    if (!tournament.TryGetProperty("events", out var events)) continue;

                    foreach (var ev in events.EnumerateArray())
                    {
                        try { ParseEvent(ev, leagueName, fixturesById); }
                        catch (Exception ex)
                        {
                            logger.LogTrace(ex, "Skipping event parse error on page {Page}", page);
                        }
                    }
                }
            }

            return (page, tournamentCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch SportyBet page {Page}", page);
            return (page, 0);
        }
    }

    private static void ParseEvent(
        JsonElement ev,
        string league,
        Dictionary<string, FixtureBuilder> map)
    {
        var eventId = ev.GetProperty("eventId").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(eventId)) return;

        var homeTeam = ev.TryGetProperty("homeTeamName", out var h) ? h.GetString() ?? "" : "";
        var awayTeam = ev.TryGetProperty("awayTeamName", out var a) ? a.GetString() ?? "" : "";

        DateTime? kickoffUtc = null;
        if (ev.TryGetProperty("estimateStartTime", out var est) && est.TryGetInt64(out var ms))
            kickoffUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

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
                    ParseOutcomes(outcomes, builder.Market1X2, builder.OutcomeIds1X2);
                    break;
                case MarketOverUnder when specifier == "total=2.5":
                    ParseOutcomes(outcomes, builder.MarketOver25, builder.OutcomeIdsOver25);
                    break;
                case MarketBtts:
                    ParseOutcomes(outcomes, builder.MarketBtts, builder.OutcomeIdsBtts);
                    break;
                case MarketDoubleChance:
                    ParseDcOutcomes(outcomes, builder);
                    break;
            }
        }
    }

    private static void ParseOutcomes(
        JsonElement outcomes,
        Dictionary<string, decimal> target,
        Dictionary<string, string> outcomeIds)
    {
        foreach (var o in outcomes.EnumerateArray())
        {
            var id = o.TryGetProperty("id", out var oid) ? oid.GetString() ?? "" : "";
            var desc = o.TryGetProperty("desc", out var d) ? d.GetString() ?? "" : "";
            var key = NormalizeOutcomeKey(id, desc);
            var odds = TryParseDecimalOdds(o);
            if (odds > 1m)
            {
                target[key] = odds;
                outcomeIds[key] = id;
            }
        }
    }

    private static void ParseDcOutcomes(JsonElement outcomes, FixtureBuilder builder)
    {
        foreach (var o in outcomes.EnumerateArray())
        {
            var id = o.TryGetProperty("id", out var oid) ? oid.GetString() ?? "" : "";
            var desc = o.TryGetProperty("desc", out var d) ? d.GetString() ?? "" : "";
            var upper = desc.ToUpperInvariant();
            string key = id switch
            {
                "9" => "1X",
                "10" => "X2",
                "11" => "12",
                _ => upper.Contains("1X") || (upper.Contains("HOME") && upper.Contains("DRAW")) ? "1X"
                    : upper.Contains("X2") || (upper.Contains("DRAW") && upper.Contains("AWAY")) ? "X2"
                    : upper.Contains("12") || (upper.Contains("HOME") && upper.Contains("AWAY")) ? "12"
                    : ""
            };
            if (string.IsNullOrEmpty(key)) continue;
            switch (key)
            {
                case "1X": builder.Dc1XOutcomeId = id; break;
                case "X2": builder.DcX2OutcomeId = id; break;
                case "12": builder.Dc12OutcomeId = id; break;
            }
        }
    }

    private static string NormalizeOutcomeKey(string id, string desc) =>
        id switch
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
        public Dictionary<string, string> OutcomeIds1X2 { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> OutcomeIdsOver25 { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> OutcomeIdsBtts { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Dc1XOutcomeId { get; set; } = "";
        public string DcX2OutcomeId { get; set; } = "";
        public string Dc12OutcomeId { get; set; } = "";

        public FixtureOddsDto ToDto()
        {
            var markets = new List<MarketOddsDto>();

            if (Market1X2.Count > 0)
                markets.Add(new MarketOddsDto
                {
                    Market = "1X2",
                    Outcomes = Market1X2.Select(kv => new OutcomeOddsDto
                    {
                        Outcome = MarketOutcomeNormalizer.NormalizeOutcome("1X2", kv.Key),
                        Odds = kv.Value
                    }).ToList()
                });

            if (MarketOver25.TryGetValue("Over2.5", out var over25) && over25 > 1m)
                markets.Add(new MarketOddsDto
                {
                    Market = "Over2.5",
                    Outcomes = [new OutcomeOddsDto { Outcome = "Over2.5", Odds = over25 }]
                });

            if (MarketOver25.TryGetValue("Under2.5", out var under25) && under25 > 1m)
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

            if (Market1X2.TryGetValue("Home", out var homeOdds) &&
                Market1X2.TryGetValue("Draw", out var drawOdds) &&
                Market1X2.TryGetValue("Away", out var awayOdds))
            {
                var hd = HarmonicMean(homeOdds, drawOdds);
                var da = HarmonicMean(drawOdds, awayOdds);
                var ha = HarmonicMean(homeOdds, awayOdds);
                var dcOutcomes = new List<OutcomeOddsDto>();
                if (hd > 1m) dcOutcomes.Add(new OutcomeOddsDto { Outcome = "1X", Odds = hd });
                if (da > 1m) dcOutcomes.Add(new OutcomeOddsDto { Outcome = "X2", Odds = da });
                if (ha > 1m) dcOutcomes.Add(new OutcomeOddsDto { Outcome = "12", Odds = ha });
                if (dcOutcomes.Count > 0)
                    markets.Add(new MarketOddsDto { Market = "DoubleChance", Outcomes = dcOutcomes });

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

        public SportyBetPlacementFixture ToPlacementFixture() => new()
        {
            EventId = EventId,
            HomeTeam = HomeTeam,
            AwayTeam = AwayTeam,
            KickoffUtc = KickoffUtc,
            HomeOutcomeId = OutcomeIds1X2.GetValueOrDefault("Home", ""),
            DrawOutcomeId = OutcomeIds1X2.GetValueOrDefault("Draw", ""),
            AwayOutcomeId = OutcomeIds1X2.GetValueOrDefault("Away", ""),
            Over25OutcomeId = OutcomeIdsOver25.GetValueOrDefault("Over2.5", ""),
            Under25OutcomeId = OutcomeIdsOver25.GetValueOrDefault("Under2.5", ""),
            BttsYesOutcomeId = OutcomeIdsBtts.GetValueOrDefault("Yes", ""),
            BttsNoOutcomeId = OutcomeIdsBtts.GetValueOrDefault("No", ""),
            Dc1XOutcomeId = Dc1XOutcomeId,
            DcX2OutcomeId = DcX2OutcomeId,
            Dc12OutcomeId = Dc12OutcomeId
        };

        private static decimal HarmonicMean(decimal a, decimal b)
        {
            if (a <= 1m || b <= 1m) return 0m;
            var combined = 1m / a + 1m / b;
            return combined > 0 ? Math.Round(1m / combined, 3) : 0m;
        }
    }
}
