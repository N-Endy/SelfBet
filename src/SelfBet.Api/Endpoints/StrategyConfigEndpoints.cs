using SelfBet.Application.Abstractions;
using SelfBet.Application.UseCases;
using SelfBet.Domain.Entities;

namespace SelfBet.Api.Endpoints;

public static class StrategyConfigEndpoints
{
    public static IEndpointRouteBuilder MapStrategyConfigEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/strategy-config").WithTags("StrategyConfig");

        group.MapGet("/", async (IStrategyConfigRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetAsync(ct)));

        group.MapPut("/", async (StrategyConfig update, UpdateStrategyConfigUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(update, ct)));

        group.MapPost("/validate", (StrategyConfig config) =>
        {
            var errors = new List<string>();
            if (config.OddsRange.Min <= 1m)
            {
                errors.Add("OddsRange.Min must be greater than 1.");
            }
            if (config.OddsRange.Max < config.OddsRange.Min)
            {
                errors.Add("OddsRange.Max must be >= OddsRange.Min.");
            }
            if (config.StakePercentagePerSlip is <= 0 or > 0.1m)
            {
                errors.Add("StakePercentagePerSlip must be in (0, 0.1].");
            }
            if (config.SlipsPerDay is < 1 or > 5)
            {
                errors.Add("SlipsPerDay must be between 1 and 5.");
            }
            return errors.Count == 0
                ? Results.Ok(new { valid = true })
                : Results.BadRequest(new { valid = false, errors });
        });

        return app;
    }
}
