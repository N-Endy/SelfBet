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
            var result = await useCase.ExecuteAsync(slipId, ct);
            return result.Ok
                ? Results.Ok(new { slipId, status = "ok", message = result.Message })
                : Results.BadRequest(new { slipId, status = "failed", message = result.Message });
        });

        group.MapPost("/{slipId:guid}/cancel", async (Guid slipId, CancelSlipUseCase useCase, CancellationToken ct) =>
        {
            var result = await useCase.ExecuteAsync(slipId, ct);
            return result.Ok
                ? Results.Ok(new { slipId, status = "cancelled", message = result.Message })
                : Results.BadRequest(new { slipId, status = "not-cancellable", message = result.Message });
        });

        return app;
    }
}
