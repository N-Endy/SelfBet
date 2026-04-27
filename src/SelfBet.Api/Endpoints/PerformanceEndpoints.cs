using SelfBet.Application.Abstractions;
using SelfBet.Application.UseCases;

namespace SelfBet.Api.Endpoints;

public static class PerformanceEndpoints
{
    public static IEndpointRouteBuilder MapPerformanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/performance").WithTags("Performance");

        group.MapGet("/summary", async (PerformanceQuery query, int? rangeDays, CancellationToken ct) =>
            Results.Ok(await query.GetSummaryAsync(rangeDays ?? 30, ct)));

        group.MapGet("/history", async (ISlipRepository repo, int? limit, CancellationToken ct) =>
            Results.Ok(await repo.GetRecentAsync(limit ?? 50, ct)));

        return app;
    }
}
