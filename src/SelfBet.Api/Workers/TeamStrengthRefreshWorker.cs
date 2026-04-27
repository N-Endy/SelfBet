using Microsoft.Extensions.Options;
using SelfBet.Application.Abstractions;
using SelfBet.Application.UseCases;
using SelfBet.Infrastructure.Providers;

namespace SelfBet.Api.Workers;

/// <summary>
/// Refreshes the historical-match dataset and refits the Dixon-Coles team
/// strengths once on startup, then daily at 03:00 UTC. The startup pass
/// guarantees freshly-deployed instances reach a usable state without waiting
/// 24 hours for the first scheduled refresh.
/// </summary>
public sealed class TeamStrengthRefreshWorker(
    ILogger<TeamStrengthRefreshWorker> logger,
    IServiceScopeFactory scopeFactory,
    IOptions<ApiFootballOptions> apiFootballOptions) : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(apiFootballOptions.Value.ApiKey))
        {
            logger.LogInformation(
                "TeamStrengthRefreshWorker disabled: no ApiFootball__ApiKey configured. " +
                "Predictions will use the bookmaker-derived fallback model.");
            return;
        }

        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try { await Task.Delay(RefreshInterval, stoppingToken); }
            catch (TaskCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var provider = sp.GetRequiredService<IHistoricalMatchProvider>();
            var historicalRepo = sp.GetRequiredService<IHistoricalMatchRepository>();
            var strengthService = sp.GetRequiredService<ITeamStrengthService>();

            if (!provider.IsConfigured) return;

            var leagues = apiFootballOptions.Value.LeagueIdMap.Keys.ToList();
            var seasons = BuildSeasonRange(apiFootballOptions.Value.SeasonsToFetch);

            logger.LogInformation(
                "TeamStrengthRefreshWorker: fetching {LeagueCount} leagues × {SeasonCount} seasons",
                leagues.Count, seasons.Count);

            foreach (var league in leagues)
            {
                if (ct.IsCancellationRequested) return;

                var fetched = await provider.FetchAsync(league, seasons, ct);
                if (fetched.Count == 0) continue;
                await historicalRepo.UpsertManyAsync(fetched, ct);

                var fitted = await strengthService.RefitLeagueAsync(league, ct);
                logger.LogInformation("League {League}: stored {Stored} matches, fitted {Fitted} teams",
                    league, fetched.Count, fitted);
            }

            logger.LogInformation("TeamStrengthRefreshWorker: refresh cycle complete.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Team strength refresh cycle failed");
        }
    }

    private static IReadOnlyList<string> BuildSeasonRange(int count)
    {
        var year = DateTime.UtcNow.Year;
        var month = DateTime.UtcNow.Month;
        var currentSeason = month >= 7 ? year : year - 1;

        var seasons = new List<string>();
        for (var i = 0; i < Math.Max(1, count); i++)
        {
            seasons.Add((currentSeason - i).ToString());
        }
        return seasons;
    }
}
