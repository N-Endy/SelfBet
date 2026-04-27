using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SelfBet.Automation.Models;
using SelfBet.Domain.Entities;

namespace SelfBet.Automation.Clients;

/// <summary>
/// Generates a SportyBet share/booking code via POST /api/ng/orders/share.
/// No login required. The returned share code can be loaded in the SportyBet
/// app to review and place the slip with a single tap.
/// </summary>
public sealed class SportyBetBookingClient(
    IHttpClientFactory httpClientFactory,
    SportyBetOptions options,
    ILogger<SportyBetBookingClient> logger)
{
    private static readonly Regex NonWordRegex = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly HashSet<string> NoiseWords =
        ["fc", "cf", "sc", "afc", "club", "the", "de", "da", "do", "cd", "ud", "ac", "as",
         "fk", "sk", "nk", "if", "bk", "city", "united", "athletic"];

    public async Task<BookingResult> GenerateBookingCodeAsync(
        Slip slip,
        IReadOnlyList<SportyBetFixtureInfo> fixtures,
        CancellationToken ct)
    {
        var selectedOutcomes = new List<SportyBetSelection>();

        foreach (var leg in slip.Legs)
        {
            var fixture = FindBestFixture(fixtures, leg);
            if (fixture is null)
            {
                logger.LogWarning("Booking: no SportyBet fixture match for {Match}", leg.MatchTitle);
                continue;
            }

            var outcome = MapLegToOutcome(fixture, leg.Market, leg.Outcome);
            if (outcome is null)
            {
                logger.LogWarning("Booking: market {Market}/{Outcome} unavailable for {Match}",
                    leg.Market, leg.Outcome, leg.MatchTitle);
                continue;
            }

            selectedOutcomes.Add(outcome);
        }

        if (selectedOutcomes.Count == 0)
            return new BookingResult { Success = false, Message = "No legs could be matched to SportyBet fixtures." };

        var (code, url) = await PostShareAsync(selectedOutcomes, ct);

        if (!string.IsNullOrEmpty(code))
        {
            logger.LogInformation("Booking code generated: {Code} for {N}/{Total} legs",
                code, selectedOutcomes.Count, slip.Legs.Count);
            return new BookingResult
            {
                Success = true,
                BookingCode = code,
                BookingUrl = url ?? $"{options.BaseUrl}/ng/?shareCode={code}",
                MatchedLegs = selectedOutcomes.Count,
                TotalLegs = slip.Legs.Count
            };
        }

        return new BookingResult { Success = false, Message = "SportyBet did not return a booking code." };
    }

    public async Task<IReadOnlyList<SportyBetFixtureInfo>> FetchTodayFixturesAsync(CancellationToken ct)
    {
        var fixtureMap = new Dictionary<string, SportyBetFixtureInfo>(StringComparer.Ordinal);
        var client = CreateClient();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (var page = 1; page <= 15; page++)
        {
            var url = $"{options.BaseUrl}/api/ng/factsCenter/pcUpcomingEvents" +
                      $"?sportId=sr:sport:1&marketId=1,18,29" +
                      $"&pageSize=100&pageNum={page}&todayGames=true&timeline=2.9&_t={ts}";

            JsonDocument doc;
            try
            {
                var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) break;
                var body = await response.Content.ReadAsStringAsync(ct);
                doc = JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch fixtures page {Page}", page);
                break;
            }

            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("tournaments", out var tournaments)) break;

            var count = 0;
            foreach (var tournament in tournaments.EnumerateArray())
            {
                count++;
                if (!tournament.TryGetProperty("events", out var events)) continue;
                foreach (var ev in events.EnumerateArray())
                {
                    try { ParseFixture(ev, tournament, fixtureMap); }
                    catch { /* ignore individual parse errors */ }
                }
            }

            if (count == 0) break;
        }

        return fixtureMap.Values.ToList();
    }

    private static void ParseFixture(
        JsonElement ev,
        JsonElement tournament,
        Dictionary<string, SportyBetFixtureInfo> map)
    {
        var eventId = ev.GetProperty("eventId").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(eventId)) return;

        var home = ev.TryGetProperty("homeTeamName", out var h) ? h.GetString() ?? "" : "";
        var away = ev.TryGetProperty("awayTeamName", out var a) ? a.GetString() ?? "" : "";

        DateTime? kickoff = null;
        if (ev.TryGetProperty("estimateStartTime", out var est) && est.TryGetInt64(out var ms))
            kickoff = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

        var fixture = new SportyBetFixtureInfo
        {
            EventId = eventId, HomeTeam = home, AwayTeam = away, KickoffUtc = kickoff
        };

        if (ev.TryGetProperty("markets", out var markets))
        {
            foreach (var market in markets.EnumerateArray())
            {
                var marketId = market.TryGetProperty("id", out var mid) ? mid.GetString() ?? "" : "";
                var specifier = market.TryGetProperty("specifier", out var spec) ? spec.GetString() ?? "" : "";
                if (!market.TryGetProperty("outcomes", out var outcomes)) continue;

                foreach (var o in outcomes.EnumerateArray())
                {
                    var oid = o.TryGetProperty("id", out var oi) ? oi.GetString() ?? "" : "";
                    switch (marketId)
                    {
                        case "1":
                            if (oid == "1") fixture.HomeOutcomeId = oid;
                            else if (oid == "2") fixture.DrawOutcomeId = oid;
                            else if (oid == "3") fixture.AwayOutcomeId = oid;
                            break;
                        case "18" when specifier == "total=2.5":
                            if (oid == "12") fixture.Over25OutcomeId = oid;
                            else if (oid == "13") fixture.Under25OutcomeId = oid;
                            break;
                        case "29":
                            if (oid == "74") fixture.BttsYesOutcomeId = oid;
                            else if (oid == "76") fixture.BttsNoOutcomeId = oid;
                            break;
                    }
                }
            }
        }

        map[eventId] = fixture;
    }

    private SportyBetFixtureInfo? FindBestFixture(
        IReadOnlyList<SportyBetFixtureInfo> fixtures,
        SlipLeg leg)
    {
        var parts = leg.MatchTitle.Split(" vs ", 2);
        if (parts.Length != 2) return null;
        var expectedHome = parts[0].Trim();
        var expectedAway = parts[1].Trim();

        var scored = fixtures
            .Select(f => new
            {
                Fixture = f,
                Score = TeamSimilarity(expectedHome, f.HomeTeam) + TeamSimilarity(expectedAway, f.AwayTeam)
            })
            .Where(x => x.Score >= 1.30)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return scored?.Fixture;
    }

    private static SportyBetSelection? MapLegToOutcome(SportyBetFixtureInfo fixture, string market, string outcome)
    {
        var (marketId, outcomeId, specifier) = (market.ToUpperInvariant(), outcome.ToUpperInvariant()) switch
        {
            ("1X2", "HOME") => ("1", fixture.HomeOutcomeId, ""),
            ("1X2", "DRAW") => ("1", fixture.DrawOutcomeId, ""),
            ("1X2", "AWAY") => ("1", fixture.AwayOutcomeId, ""),
            ("OVER2.5", "OVER2.5") => ("18", fixture.Over25OutcomeId, "total=2.5"),
            ("UNDER2.5", "UNDER2.5") => ("18", fixture.Under25OutcomeId, "total=2.5"),
            ("BTTS", "YES") => ("29", fixture.BttsYesOutcomeId, ""),
            ("BTTS", "NO") => ("29", fixture.BttsNoOutcomeId, ""),
            // DrawNoBet Home → map to 1X2 Home (safest approximation for share code)
            ("DRAWNOBBET", "HOME") or ("DRAWNOBBET", "HOME") => ("1", fixture.HomeOutcomeId, ""),
            ("DRAWNOBET", "AWAY") => ("1", fixture.AwayOutcomeId, ""),
            _ => ("", "", "")
        };

        if (string.IsNullOrEmpty(marketId) || string.IsNullOrEmpty(outcomeId)) return null;
        return new SportyBetSelection
        {
            EventId = fixture.EventId, MarketId = marketId, OutcomeId = outcomeId, Specifier = specifier
        };
    }

    private async Task<(string? Code, string? Url)> PostShareAsync(
        List<SportyBetSelection> outcomes, CancellationToken ct)
    {
        var payload = new
        {
            selections = outcomes.Select(o => new
            {
                eventId = o.EventId,
                marketId = o.MarketId,
                specifier = o.Specifier,
                outcomeId = o.OutcomeId
            }).ToArray()
        };

        var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var client = CreateClient();

        logger.LogInformation("POST /api/ng/orders/share with {N} selections", outcomes.Count);

        var response = await client.PostAsync($"{options.BaseUrl}/api/ng/orders/share", body, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        logger.LogDebug("Share response [{Status}]: {Body}",
            response.StatusCode, responseBody[..Math.Min(300, responseBody.Length)]);

        if (!response.IsSuccessStatusCode) return (null, null);

        var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("data", out var data)) return (null, null);

        string? code = null;
        string? url = null;
        if (data.TryGetProperty("shareCode", out var sc)) code = sc.GetString();
        else if (data.TryGetProperty("bookCode", out var bc)) code = bc.GetString();
        else if (data.TryGetProperty("code", out var c)) code = c.GetString();
        else if (data.ValueKind == JsonValueKind.String) code = data.GetString();
        if (data.TryGetProperty("shareURL", out var su)) url = su.GetString();

        return (code, url);
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("SportyBetBooking");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"{options.BaseUrl}/ng/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", options.BaseUrl);
        client.DefaultRequestHeaders.TryAddWithoutValidation("clientid", "web");
        client.DefaultRequestHeaders.TryAddWithoutValidation("platform", "web");
        client.DefaultRequestHeaders.TryAddWithoutValidation("operid", "2");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private double TeamSimilarity(string expected, string actual)
    {
        var e = Normalize(expected);
        var a = Normalize(actual);
        if (e == a) return 1.0;
        var et = Tokenize(e);
        var at = Tokenize(a);
        if (et.Count == 0 || at.Count == 0) return 0;
        var overlap = et.Intersect(at).Count();
        var shorter = Math.Min(et.Count, at.Count);
        var ratio = shorter == 0 ? 0 : overlap / (double)shorter;
        if (e.Replace(" ", "") == a.Replace(" ", "")) ratio = Math.Max(ratio, 0.9);
        if (et.FirstOrDefault() == at.FirstOrDefault() && overlap > 0) ratio = Math.Max(ratio, 0.78);
        return Math.Min(1.0, ratio);
    }

    private string Normalize(string v) =>
        string.Join(" ", NonWordRegex.Replace(v.ToLowerInvariant(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private List<string> Tokenize(string v) =>
        v.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !NoiseWords.Contains(w)).ToList();
}

// ── Supporting types ──────────────────────────────────────────────────────

public sealed class BookingResult
{
    public bool Success { get; init; }
    public string? BookingCode { get; init; }
    public string? BookingUrl { get; init; }
    public string? Message { get; init; }
    public int MatchedLegs { get; init; }
    public int TotalLegs { get; init; }
}

public sealed class SportyBetFixtureInfo
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
}

public sealed class SportyBetSelection
{
    public string EventId { get; init; } = "";
    public string MarketId { get; init; } = "";
    public string OutcomeId { get; init; } = "";
    public string Specifier { get; init; } = "";
}
