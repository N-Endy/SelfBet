using SelfBet.Application.Abstractions;
using SelfBet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SelfBet.Api.Endpoints;

public static class PredictionEndpoints
{
    public static IEndpointRouteBuilder MapPredictionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/predictions").WithTags("Predictions");

        // GET /api/predictions/strengths — list all fitted team strengths grouped by league
        group.MapGet("/strengths", async (SelfBetDbContext db, CancellationToken ct) =>
        {
            var profiles = await db.LeagueStrengthProfiles
                .AsNoTracking()
                .OrderBy(p => p.League)
                .ToListAsync(ct);

            var teams = await db.TeamStrengths
                .AsNoTracking()
                .OrderBy(t => t.League).ThenByDescending(t => t.Attack)
                .ToListAsync(ct);

            var grouped = profiles.Select(p => new
            {
                p.League,
                p.AvgHomeGoals,
                p.AvgAwayGoals,
                p.HomeAdvantage,
                p.DixonColesRho,
                p.SampleSize,
                p.FittedAtUtc,
                Teams = teams.Where(t => t.League == p.League).Select(t => new
                {
                    t.Team,
                    t.Attack,
                    t.Defence,
                    t.SampleSize
                })
            });

            return Results.Ok(grouped);
        });

        // POST /api/predictions/refresh — trigger an out-of-band team-strength refit
        // Useful immediately after deploying or changing the league map.
        group.MapPost("/refresh", async (
            IHistoricalMatchProvider provider,
            IHistoricalMatchRepository historicalRepo,
            ITeamStrengthService strengthService,
            Microsoft.Extensions.Options.IOptions<SelfBet.Infrastructure.Providers.ApiFootballOptions> opts,
            CancellationToken ct) =>
        {
            if (!provider.IsConfigured)
            {
                return Results.BadRequest(new { message = "API-Football key not configured." });
            }

            var leagues = opts.Value.LeagueIdMap.Keys.ToList();
            var seasons = BuildSeasons(opts.Value.SeasonsToFetch);
            var report = new List<object>();

            foreach (var league in leagues)
            {
                if (ct.IsCancellationRequested) break;
                var fetched = await provider.FetchAsync(league, seasons, ct);
                if (fetched.Count > 0) await historicalRepo.UpsertManyAsync(fetched, ct);
                var fitted = await strengthService.RefitLeagueAsync(league, ct);
                report.Add(new { league, fetched = fetched.Count, fittedTeams = fitted });
            }

            return Results.Ok(new { leagues = report });
        });

        return app;
    }

    private static IReadOnlyList<string> BuildSeasons(int count)
    {
        var year = DateTime.UtcNow.Year;
        var month = DateTime.UtcNow.Month;
        var currentSeason = month >= 7 ? year : year - 1;
        var list = new List<string>();
        for (var i = 0; i < Math.Max(1, count); i++) list.Add((currentSeason - i).ToString());
        return list;
    }
}
