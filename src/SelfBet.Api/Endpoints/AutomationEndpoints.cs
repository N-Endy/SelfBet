using SelfBet.Application.Abstractions;
using SelfBet.Application.UseCases;
using SelfBet.Automation.Clients;

namespace SelfBet.Api.Endpoints;

public static class AutomationEndpoints
{
    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/automation").WithTags("Automation");

        group.MapPost("/start", async (ToggleAutomationUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(true, ct)));

        group.MapPost("/stop", async (ToggleAutomationUseCase useCase, CancellationToken ct) =>
            Results.Ok(await useCase.ExecuteAsync(false, ct)));

        group.MapGet("/status", async (IStrategyConfigRepository repo, CancellationToken ct) =>
        {
            var config = await repo.GetAsync(ct);
            return Results.Ok(new { config.AutomationEnabled, config.RequireConfirmationOnRisk, config.UpdatedAtUtc });
        });

        // OTP submission — called from dashboard when SportyBet requires SMS verification
        group.MapPost("/otp", async (HttpContext ctx, SportyBetAuthClient authClient, CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<OtpRequest>(ct);
            if (string.IsNullOrWhiteSpace(body?.Otp))
                return Results.BadRequest("otp is required.");

            var result = await authClient.SubmitOtpAsync(body.Otp, ct);
            return result.Success
                ? Results.Ok(new { message = "OTP verified. You can now place slips." })
                : Results.BadRequest(new { message = result.Message });
        });

        return app;
    }

    private sealed record OtpRequest(string? Otp);
}
