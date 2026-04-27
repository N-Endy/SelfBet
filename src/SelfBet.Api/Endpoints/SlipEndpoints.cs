using SelfBet.Application.Abstractions;
using SelfBet.Application.UseCases;

namespace SelfBet.Api.Endpoints;

public static class SlipEndpoints
{
    public static IEndpointRouteBuilder MapSlipEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/slips").WithTags("Slips");

        group.MapGet("/today", async (ISlipRepository repo, CancellationToken ct) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return Results.Ok(await repo.GetByDateAsync(today, ct));
        });

        group.MapGet("/{slipId:guid}", async (Guid slipId, ISlipRepository repo, CancellationToken ct) =>
        {
            var slip = await repo.GetByIdAsync(slipId, ct);
            return slip is null ? Results.NotFound() : Results.Ok(slip);
        });

        group.MapPost("/{slipId:guid}/place", async (Guid slipId, PlaceSlipUseCase useCase, CancellationToken ct) =>
        {
            var success = await useCase.ExecuteAsync(slipId, ct);
            return success ? Results.Ok(new { slipId, status = "placed" }) : Results.BadRequest(new { slipId, status = "failed" });
        });

        group.MapPost("/{slipId:guid}/cancel", async (Guid slipId, CancelSlipUseCase useCase, CancellationToken ct) =>
        {
            var success = await useCase.ExecuteAsync(slipId, ct);
            return success ? Results.Ok(new { slipId, status = "cancelled" }) : Results.BadRequest(new { slipId, status = "not-cancellable" });
        });

        return app;
    }
}
