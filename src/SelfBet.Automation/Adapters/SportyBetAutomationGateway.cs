using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Automation.Clients;
using SelfBet.Automation.Models;
using SelfBet.Domain.Entities;

namespace SelfBet.Automation.Adapters;

/// <summary>
/// Production automation gateway.
///
/// Mode: booking_code (default)
///   → Fetches today's fixtures from SportyBet, maps legs to outcome IDs,
///     calls POST /api/ng/orders/share, returns a share code.
///     No credentials needed. User taps the link in the dashboard to load+place in the app.
///
/// Mode: full_auth
///   → Logs into SportyBet, resolves fixtures, calls POST /api/ng/orders/create.
///     Takes stake immediately from account.
///
/// Gracefully falls back to booking_code if full_auth login fails.
/// </summary>
public sealed class SportyBetAutomationGateway(
    SportyBetBookingClient bookingClient,
    SportyBetAuthClient authClient,
    SportyBetOptions options,
    ILogger<SportyBetAutomationGateway> logger) : IAutomationGateway
{
    // Cache today's fixture list so we only fetch once per run
    private IReadOnlyList<SportyBetFixtureInfo>? _todayFixtures;
    private DateTimeOffset _fixturesFetchedAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan FixturesCacheTtl = TimeSpan.FromMinutes(20);

    public async Task<PlacementAttempt> PlaceSlipAsync(Slip slip, CancellationToken cancellationToken)
    {
        if (options.DryRun)
        {
            logger.LogInformation("[DryRun] Would place slip {Id} with {N} legs", slip.Id, slip.Legs.Count);
            return new PlacementAttempt
            {
                SlipId = slip.Id,
                Success = true,
                PlacementMode = "dry_run",
                BookingCode = "DRY-RUN",
                BookingUrl = $"{options.BaseUrl}/ng/?shareCode=DRYRUN"
            };
        }

        var fixtures = await GetTodayFixturesAsync(cancellationToken);

        if (string.Equals(options.PlacementMode, "full_auth", StringComparison.OrdinalIgnoreCase))
        {
            return await PlaceFullAuthAsync(slip, fixtures, cancellationToken);
        }

        return await PlaceBookingCodeAsync(slip, fixtures, cancellationToken);
    }

    private async Task<PlacementAttempt> PlaceBookingCodeAsync(
        Slip slip,
        IReadOnlyList<SportyBetFixtureInfo> fixtures,
        CancellationToken ct)
    {
        try
        {
            var result = await bookingClient.GenerateBookingCodeAsync(slip, fixtures, ct);

            if (result.Success)
            {
                return new PlacementAttempt
                {
                    SlipId = slip.Id,
                    Success = true,
                    PlacementMode = "booking_code",
                    BookingCode = result.BookingCode,
                    BookingUrl = result.BookingUrl
                };
            }

            logger.LogWarning("Booking code generation failed: {Msg}", result.Message);
            return new PlacementAttempt
            {
                SlipId = slip.Id,
                Success = false,
                PlacementMode = "booking_code",
                Error = result.Message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating booking code for slip {Id}", slip.Id);
            return new PlacementAttempt
            {
                SlipId = slip.Id, Success = false, PlacementMode = "booking_code", Error = ex.Message
            };
        }
    }

    private async Task<PlacementAttempt> PlaceFullAuthAsync(
        Slip slip,
        IReadOnlyList<SportyBetFixtureInfo> fixtures,
        CancellationToken ct)
    {
        // First generate a booking code (so user always has a fallback)
        var bookingResult = await bookingClient.GenerateBookingCodeAsync(slip, fixtures, ct);

        // Try full auth login
        var loginResult = await authClient.LoginAsync(ct);
        if (!loginResult.Success)
        {
            logger.LogWarning("Full-auth login failed, returning booking code only. {Msg}", loginResult.Message);
            return new PlacementAttempt
            {
                SlipId = slip.Id,
                Success = bookingResult.Success,
                PlacementMode = "booking_code_fallback",
                BookingCode = bookingResult.BookingCode,
                BookingUrl = bookingResult.BookingUrl,
                Error = loginResult.RequiresOtp ? "OTP required" : loginResult.Message
            };
        }

        // Build selections for full auth placement
        var selections = BuildSelections(slip, fixtures);
        if (selections.Count == 0)
        {
            return new PlacementAttempt
            {
                SlipId = slip.Id, Success = false, PlacementMode = "full_auth",
                Error = "No legs could be matched to live fixtures."
            };
        }

        await Task.Delay(options.PacingDelayMs, ct);

        var orderResult = await authClient.PlaceOrderAsync(selections, slip.Stake, ct);

        if (orderResult.Success)
        {
            logger.LogInformation("Full-auth order placed. TicketId={Id}", orderResult.TicketId);
            return new PlacementAttempt
            {
                SlipId = slip.Id,
                Success = true,
                PlacementMode = "full_auth",
                ExternalTicketId = orderResult.TicketId,
                BookingCode = bookingResult.BookingCode,
                BookingUrl = bookingResult.BookingUrl
            };
        }

        // Order failed — return booking code as fallback
        logger.LogWarning("Full-auth order failed: {Msg}. Falling back to booking code.", orderResult.Message);
        return new PlacementAttempt
        {
            SlipId = slip.Id,
            Success = bookingResult.Success,
            PlacementMode = "full_auth_fallback",
            BookingCode = bookingResult.BookingCode,
            BookingUrl = bookingResult.BookingUrl,
            Error = orderResult.Message
        };
    }

    private static List<SportyBetSelection> BuildSelections(
        Slip slip, IReadOnlyList<SportyBetFixtureInfo> fixtures)
    {
        var result = new List<SportyBetSelection>();
        foreach (var leg in slip.Legs)
        {
            var parts = leg.MatchTitle.Split(" vs ", 2);
            if (parts.Length != 2) continue;
            var home = parts[0].Trim();
            var away = parts[1].Trim();
            var fixture = fixtures.FirstOrDefault(f =>
                NormTeam(f.HomeTeam) == NormTeam(home) &&
                NormTeam(f.AwayTeam) == NormTeam(away));
            if (fixture is null) continue;

            var (marketId, outcomeId, specifier) = (leg.Market.ToUpperInvariant(), leg.Outcome.ToUpperInvariant()) switch
            {
                ("1X2", "HOME") => ("1", fixture.HomeOutcomeId, ""),
                ("1X2", "DRAW") => ("1", fixture.DrawOutcomeId, ""),
                ("1X2", "AWAY") => ("1", fixture.AwayOutcomeId, ""),
                ("OVER2.5", _) => ("18", fixture.Over25OutcomeId, "total=2.5"),
                ("UNDER2.5", _) => ("18", fixture.Under25OutcomeId, "total=2.5"),
                ("BTTS", "YES") => ("29", fixture.BttsYesOutcomeId, ""),
                ("BTTS", "NO") => ("29", fixture.BttsNoOutcomeId, ""),
                _ => ("", "", "")
            };

            if (!string.IsNullOrEmpty(marketId) && !string.IsNullOrEmpty(outcomeId))
                result.Add(new SportyBetSelection { EventId = fixture.EventId, MarketId = marketId, OutcomeId = outcomeId, Specifier = specifier });
        }

        return result;
    }

    public async Task<decimal?> ReadAccountBalanceAsync(CancellationToken cancellationToken)
    {
        try { return await authClient.GetBalanceAsync(cancellationToken); }
        catch { return null; }
    }

    private async Task<IReadOnlyList<SportyBetFixtureInfo>> GetTodayFixturesAsync(CancellationToken ct)
    {
        if (_todayFixtures is not null && DateTimeOffset.UtcNow - _fixturesFetchedAt < FixturesCacheTtl)
            return _todayFixtures;

        _todayFixtures = await bookingClient.FetchTodayFixturesAsync(ct);
        _fixturesFetchedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("Fetched {Count} live fixtures from SportyBet", _todayFixtures.Count);
        return _todayFixtures;
    }

    private static string NormTeam(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]", "");
}
