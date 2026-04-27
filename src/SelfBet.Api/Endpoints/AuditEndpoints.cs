using SelfBet.Application.Abstractions;

namespace SelfBet.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-events").WithTags("Audit");

        group.MapGet("/", async (IAuditService audit, int? limit, CancellationToken ct) =>
            Results.Ok(await audit.GetRecentAsync(limit ?? 100, ct)));

        return app;
    }
}
