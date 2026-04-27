using SelfBet.Application.Abstractions;
using SelfBet.Application.UseCases;

namespace SelfBet.Api.Endpoints;

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/runs").WithTags("Runs");

        group.MapPost("/execute-now", async (RunEngine engine, CancellationToken ct) =>
        {
            var outcome = await engine.ExecuteAsync("manual", ct);
            return Results.Ok(outcome);
        });

        group.MapGet("/", async (IRunRepository repo, int? limit, CancellationToken ct) =>
            Results.Ok(await repo.GetRecentAsync(limit ?? 25, ct)));

        group.MapGet("/{runId:guid}", async (Guid runId, IRunRepository repo, ISlipRepository slips, CancellationToken ct) =>
        {
            var run = await repo.GetByIdAsync(runId, ct);
            if (run is null)
            {
                return Results.NotFound();
            }
            var runSlips = await slips.GetByRunAsync(runId, ct);
            return Results.Ok(new { Run = run, Slips = runSlips });
        });

        return app;
    }
}
