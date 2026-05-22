using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Automation.Clients;
using SelfBet.Automation.Models;
using SelfBet.Domain.Entities;

namespace SelfBet.Automation.Adapters;

public sealed class SportyBetAutomationGateway(
    SportyBetBookingClient bookingClient,
    SportyBetAuthClient authClient,
    ISportyBetFixtureCache fixtureCache,
    SportyBetOptions options,
    ILogger<SportyBetAutomationGateway> logger) : IAutomationGateway
{
    public async Task<PlacementAttempt> PlaceSlipAsync(
        Slip slip,
        CancellationToken cancellationToken,
        IReadOnlyList<SportyBetPlacementFixture>? placementFixtures = null)
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

        placementFixtures ??= (await fixtureCache.GetSnapshotAsync(cancellationToken)).PlacementFixtures;

        if (string.Equals(options.PlacementMode, "full_auth", StringComparison.OrdinalIgnoreCase))
            return await PlaceFullAuthAsync(slip, placementFixtures, cancellationToken);

        return await PlaceBookingCodeAsync(slip, placementFixtures, cancellationToken);
    }

    private async Task<PlacementAttempt> PlaceBookingCodeAsync(
        Slip slip,
        IReadOnlyList<SportyBetPlacementFixture> fixtures,
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
        IReadOnlyList<SportyBetPlacementFixture> fixtures,
        CancellationToken ct)
    {
        var selections = BuildSelections(slip, fixtures);
        if (selections.Count == 0)
        {
            return new PlacementAttempt
            {
                SlipId = slip.Id, Success = false, PlacementMode = "full_auth",
                Error = "No legs could be matched to live fixtures."
            };
        }

        var loginResult = await authClient.LoginAsync(ct);
        if (!loginResult.Success)
        {
            var fallback = await bookingClient.GenerateBookingCodeAsync(slip, fixtures, ct);
            return new PlacementAttempt
            {
                SlipId = slip.Id,
                Success = fallback.Success,
                PlacementMode = "booking_code_fallback",
                BookingCode = fallback.BookingCode,
                BookingUrl = fallback.BookingUrl,
                Error = loginResult.RequiresOtp ? "OTP required" : loginResult.Message
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
                ExternalTicketId = orderResult.TicketId
            };
        }

        logger.LogWarning("Full-auth order failed: {Msg}. Falling back to booking code.", orderResult.Message);
        var bookingFallback = await bookingClient.GenerateBookingCodeAsync(slip, fixtures, ct);
        return new PlacementAttempt
        {
            SlipId = slip.Id,
            Success = bookingFallback.Success,
            PlacementMode = "full_auth_fallback",
            BookingCode = bookingFallback.BookingCode,
            BookingUrl = bookingFallback.BookingUrl,
            Error = orderResult.Message
        };
    }

    private static List<SportyBetSelection> BuildSelections(
        Slip slip, IReadOnlyList<SportyBetPlacementFixture> fixtures)
    {
        var result = new List<SportyBetSelection>();
        foreach (var leg in slip.Legs)
        {
            var fixture = SportyBetLegMapper.FindBestFixture(fixtures, leg);
            if (fixture is null) continue;

            var mapped = SportyBetLegMapper.MapLegToSelection(fixture, leg.Market, leg.Outcome);
            if (mapped is null) continue;

            result.Add(new SportyBetSelection
            {
                EventId = mapped.EventId,
                MarketId = mapped.MarketId,
                OutcomeId = mapped.OutcomeId,
                Specifier = mapped.Specifier
            });
        }

        return result;
    }

    public async Task<decimal?> ReadAccountBalanceAsync(CancellationToken cancellationToken)
    {
        try { return await authClient.GetBalanceAsync(cancellationToken); }
        catch { return null; }
    }
}
