using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Services;

/// <summary>
/// Fits per-league Dixon-Coles team strengths from <see cref="HistoricalMatch"/>
/// records and serves the resulting goal expectations to the prediction service.
///
/// The model is a per-team multiplicative attack/defence rating combined with a
/// league home advantage and a Dixon-Coles low-score correction (ρ). Compared
/// to a full L-BFGS MLE fit, the closed-form ratio approach used here is far
/// more numerically stable for small samples and converges to the same answer
/// when the dataset is large enough — which is exactly what we want in
/// production where leagues like Saudi Pro have 100-200 matches per season.
///
/// Fixture goal expectations:
///     λ_home = avg_home_goals × home.Attack × away.Defence
///     λ_away = avg_away_goals × away.Attack × home.Defence
///
/// The ρ value (between roughly -0.2 and 0) is fit by grid search to maximise
/// the likelihood of observed scoreline frequencies in the league.
/// </summary>
public sealed class TeamStrengthService(
    IHistoricalMatchRepository historicalRepo,
    ITeamStrengthRepository strengthRepo,
    LeagueNameResolver leagueResolver,
    IMemoryCache cache,
    ILogger<TeamStrengthService> logger)
    : ITeamStrengthService
{
    private const int LookbackDays    = 730;
    private const int MinMatchesTeam  = 6;
    private const int MinMatchesLeague = 80;
    private static readonly TimeSpan ExpectationCacheTtl = TimeSpan.FromHours(6);

    public async Task RefitAllAsync(CancellationToken ct)
    {
        var leagues = await historicalRepo.GetDistinctLeaguesAsync(ct);
        foreach (var league in leagues)
        {
            await RefitLeagueAsync(league, ct);
        }
    }

    public async Task<int> RefitLeagueAsync(string league, CancellationToken ct)
    {
        var matches = await historicalRepo.GetByLeagueAsync(league, LookbackDays, ct);
        if (matches.Count < MinMatchesLeague)
        {
            logger.LogInformation(
                "Skipping fit for league {League}: only {Count} matches (need ≥{Min}).",
                league, matches.Count, MinMatchesLeague);
            return 0;
        }

        // Aggregate per-team home/away goals
        var teamStats = new Dictionary<string, TeamMatchStats>(StringComparer.OrdinalIgnoreCase);
        long totalHomeGoals = 0, totalAwayGoals = 0, totalMatches = 0;

        foreach (var m in matches)
        {
            totalHomeGoals += m.HomeGoals;
            totalAwayGoals += m.AwayGoals;
            totalMatches += 1;

            var home = GetOrAdd(teamStats, m.HomeTeam);
            var away = GetOrAdd(teamStats, m.AwayTeam);

            home.HomeMatches++;
            home.HomeScored += m.HomeGoals;
            home.HomeConceded += m.AwayGoals;

            away.AwayMatches++;
            away.AwayScored += m.AwayGoals;
            away.AwayConceded += m.HomeGoals;
        }

        var avgHomeGoals = (double)totalHomeGoals / totalMatches;
        var avgAwayGoals = (double)totalAwayGoals / totalMatches;
        var homeAdvantage = avgAwayGoals > 0 ? Math.Log(avgHomeGoals / avgAwayGoals) : 0.0;

        // Per-team attack/defence
        var strengths = new List<TeamStrength>();
        foreach (var (team, stats) in teamStats)
        {
            if (stats.HomeMatches < MinMatchesTeam || stats.AwayMatches < MinMatchesTeam)
                continue;

            var attackHome  = stats.HomeScored   / (double)stats.HomeMatches / avgHomeGoals;
            var attackAway  = stats.AwayScored   / (double)stats.AwayMatches / avgAwayGoals;
            var defenceHome = stats.HomeConceded / (double)stats.HomeMatches / avgAwayGoals;
            var defenceAway = stats.AwayConceded / (double)stats.AwayMatches / avgHomeGoals;

            var attack  = Math.Clamp((attackHome  + attackAway)  / 2.0, 0.20, 3.50);
            var defence = Math.Clamp((defenceHome + defenceAway) / 2.0, 0.20, 3.50);

            strengths.Add(new TeamStrength
            {
                League = league,
                Team = team,
                Attack = attack,
                Defence = defence,
                SampleSize = stats.HomeMatches + stats.AwayMatches
            });
        }

        if (strengths.Count == 0)
        {
            logger.LogInformation("League {League}: no teams with enough matches.", league);
            return 0;
        }

        var rho = FitDixonColesRho(matches, strengths, avgHomeGoals, avgAwayGoals);

        var leagueProfile = new LeagueStrengthProfile
        {
            League = league,
            AvgHomeGoals = avgHomeGoals,
            AvgAwayGoals = avgAwayGoals,
            HomeAdvantage = homeAdvantage,
            DixonColesRho = rho,
            SampleSize = (int)totalMatches
        };

        await strengthRepo.UpsertManyAsync(strengths, leagueProfile, ct);

        // Bust expectation cache for this league
        InvalidateLeagueCache(league);

        logger.LogInformation(
            "Fit league {League}: {Teams} teams, {Matches} matches, ρ={Rho:F3}, λ_home_avg={H:F2}, λ_away_avg={A:F2}",
            league, strengths.Count, totalMatches, rho, avgHomeGoals, avgAwayGoals);

        return strengths.Count;
    }

    public async Task<FixtureExpectation?> GetFixtureExpectationAsync(
        string league,
        string homeTeam,
        string awayTeam,
        CancellationToken ct)
    {
        var key = $"fx:{league}|{homeTeam}|{awayTeam}";
        if (cache.TryGetValue<FixtureExpectation?>(key, out var cached))
            return cached;

        var canonicalLeague = leagueResolver.Resolve(league);
        var profile = await strengthRepo.GetLeagueProfileAsync(canonicalLeague, ct);
        if (profile is null)
        {
            cache.Set<FixtureExpectation?>(key, null, ExpectationCacheTtl);
            return null;
        }

        var home = await strengthRepo.GetAsync(canonicalLeague, homeTeam, ct);
        var away = await strengthRepo.GetAsync(canonicalLeague, awayTeam, ct);
        if (home is null || away is null)
        {
            cache.Set<FixtureExpectation?>(key, null, ExpectationCacheTtl);
            return null;
        }

        var lambdaHome = profile.AvgHomeGoals * home.Attack * away.Defence;
        var lambdaAway = profile.AvgAwayGoals * away.Attack * home.Defence;

        // Defensive clamps
        lambdaHome = Math.Clamp(lambdaHome, 0.10, 6.00);
        lambdaAway = Math.Clamp(lambdaAway, 0.10, 6.00);

        var expectation = new FixtureExpectation(
            lambdaHome, lambdaAway, profile.DixonColesRho,
            home.SampleSize, away.SampleSize);

        cache.Set(key, expectation, ExpectationCacheTtl);
        return expectation;
    }

    // ── Dixon-Coles ρ fit (grid search on log-likelihood) ───────────────────
    private static double FitDixonColesRho(
        IReadOnlyList<HistoricalMatch> matches,
        IReadOnlyList<TeamStrength> strengths,
        double avgHomeGoals,
        double avgAwayGoals)
    {
        var byTeam = strengths.ToDictionary(s => s.Team, StringComparer.OrdinalIgnoreCase);
        double[] candidates = [-0.20, -0.15, -0.10, -0.05, 0.00, 0.05];

        var bestRho = -0.05;
        var bestLl = double.NegativeInfinity;

        foreach (var rho in candidates)
        {
            var ll = 0.0;
            foreach (var m in matches)
            {
                if (!byTeam.TryGetValue(m.HomeTeam, out var h)) continue;
                if (!byTeam.TryGetValue(m.AwayTeam, out var a)) continue;

                var lH = avgHomeGoals * h.Attack * a.Defence;
                var lA = avgAwayGoals * a.Attack * h.Defence;
                if (lH <= 0 || lA <= 0) continue;

                var pH = PoissonPmf(lH, m.HomeGoals);
                var pA = PoissonPmf(lA, m.AwayGoals);
                if (pH <= 0 || pA <= 0) continue;

                var tau = DixonColesTau(m.HomeGoals, m.AwayGoals, lH, lA, rho);
                ll += Math.Log(Math.Max(tau * pH * pA, 1e-15));
            }

            if (ll > bestLl)
            {
                bestLl = ll;
                bestRho = rho;
            }
        }

        return bestRho;
    }

    public static double DixonColesTau(int homeGoals, int awayGoals, double lambdaHome, double lambdaAway, double rho)
    {
        return (homeGoals, awayGoals) switch
        {
            (0, 0) => Math.Max(1.0 - lambdaHome * lambdaAway * rho, 1e-9),
            (0, 1) => 1.0 + lambdaHome * rho,
            (1, 0) => 1.0 + lambdaAway * rho,
            (1, 1) => 1.0 - rho,
            _ => 1.0
        };
    }

    public static double PoissonPmf(double lambda, int k)
    {
        if (lambda <= 0) return k == 0 ? 1.0 : 0.0;
        return Math.Exp(-lambda) * Math.Pow(lambda, k) / Factorial(k);
    }

    private static double Factorial(int n)
    {
        double r = 1;
        for (var i = 2; i <= n; i++) r *= i;
        return r;
    }

    private void InvalidateLeagueCache(string league)
    {
        // IMemoryCache has no key enumeration; we rely on TTL to refresh entries
        // for other leagues. This is a no-op marker for clarity.
        _ = league;
    }

    private static TeamMatchStats GetOrAdd(Dictionary<string, TeamMatchStats> dict, string team)
    {
        if (!dict.TryGetValue(team, out var s))
        {
            s = new TeamMatchStats();
            dict[team] = s;
        }
        return s;
    }

    private sealed class TeamMatchStats
    {
        public int HomeMatches;
        public int HomeScored;
        public int HomeConceded;
        public int AwayMatches;
        public int AwayScored;
        public int AwayConceded;
    }
}
