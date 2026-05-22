using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Automation.Models;
using SelfBet.Domain.Entities;

namespace SelfBet.Automation.Clients;

/// <summary>
/// Generates a SportyBet share/booking code via POST /api/ng/orders/share.
/// </summary>
public sealed class SportyBetBookingClient(
    IHttpClientFactory httpClientFactory,
    ISportyBetFixtureCache fixtureCache,
    SportyBetOptions options,
    ILogger<SportyBetBookingClient> logger)
{
    public async Task<BookingResult> GenerateBookingCodeAsync(
        Slip slip,
        IReadOnlyList<SportyBetPlacementFixture>? fixtures,
        CancellationToken ct)
    {
        fixtures ??= (await fixtureCache.GetSnapshotAsync(ct)).PlacementFixtures;

        var selectedOutcomes = new List<SportyBetSelection>();

        foreach (var leg in slip.Legs)
        {
            var fixture = SportyBetLegMapper.FindBestFixture(fixtures, leg);
            if (fixture is null)
            {
                logger.LogWarning("Booking: no SportyBet fixture match for {Match}", leg.MatchTitle);
                continue;
            }

            var outcome = SportyBetLegMapper.MapLegToSelection(fixture, leg.Market, leg.Outcome);
            if (outcome is null)
            {
                logger.LogWarning("Booking: market {Market}/{Outcome} unavailable for {Match}",
                    leg.Market, leg.Outcome, leg.MatchTitle);
                continue;
            }

            selectedOutcomes.Add(new SportyBetSelection
            {
                EventId = outcome.EventId,
                MarketId = outcome.MarketId,
                OutcomeId = outcome.OutcomeId,
                Specifier = outcome.Specifier
            });
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
}

public sealed class BookingResult
{
    public bool Success { get; init; }
    public string? BookingCode { get; init; }
    public string? BookingUrl { get; init; }
    public string? Message { get; init; }
    public int MatchedLegs { get; init; }
    public int TotalLegs { get; init; }
}

public sealed class SportyBetSelection
{
    public string EventId { get; init; } = "";
    public string MarketId { get; init; } = "";
    public string OutcomeId { get; init; } = "";
    public string Specifier { get; init; } = "";
}
