using SelfBet.Application.Abstractions;

namespace SelfBet.Api.Endpoints;

public static class BankrollEndpoints
{
    public static IEndpointRouteBuilder MapBankrollEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bankroll").WithTags("Bankroll");

        group.MapGet("/current", async (IBankrollService service, CancellationToken ct) =>
            Results.Ok(await service.GetCurrentAsync(ct)));

        group.MapGet("/history", async (IBankrollRepository repo, int? limit, CancellationToken ct) =>
            Results.Ok(await repo.GetHistoryAsync(limit ?? 50, ct)));

        // Read live balance from SportyBet (requires full_auth credentials)
        group.MapGet("/live", async (IAutomationGateway gateway, IBankrollService bankrollService,
            IBankrollRepository bankrollRepo, IStrategyConfigRepository configRepo, CancellationToken ct) =>
        {
            var liveBalance = await gateway.ReadAccountBalanceAsync(ct);
            if (liveBalance is null)
                return Results.Ok(await bankrollService.GetCurrentAsync(ct));

            // Persist the fresh balance snapshot
            var config = await configRepo.GetAsync(ct);
            var stake = bankrollService.ComputeStakePerSlip(liveBalance.Value, config);
            var snapshot = await bankrollService.CaptureAsync(liveBalance.Value, stake, "live SportyBet balance", ct);
            return Results.Ok(snapshot);
        });

        group.MapPost("/snapshot", async (
            BankrollSnapshotRequest request,
            IBankrollService service,
            IStrategyConfigRepository configRepo,
            CancellationToken ct) =>
        {
            var config = await configRepo.GetAsync(ct);
            var stake = service.ComputeStakePerSlip(request.Balance, config);
            var snapshot = await service.CaptureAsync(request.Balance, stake, request.Note, ct);
            return Results.Ok(snapshot);
        });

        return app;
    }

    public sealed record BankrollSnapshotRequest(decimal Balance, string? Note);
}
