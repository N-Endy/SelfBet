using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;

namespace SelfBet.Infrastructure.Persistence;

public sealed class EfTeamStrengthRepository(SelfBetDbContext db) : ITeamStrengthRepository
{
    public async Task<TeamStrength?> GetAsync(string league, string team, CancellationToken ct) =>
        await db.TeamStrengths.AsNoTracking()
            .FirstOrDefaultAsync(t => t.League == league && t.Team == team, ct);

    public async Task<LeagueStrengthProfile?> GetLeagueProfileAsync(string league, CancellationToken ct) =>
        await db.LeagueStrengthProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.League == league, ct);

    public async Task UpsertManyAsync(
        IEnumerable<TeamStrength> teams,
        LeagueStrengthProfile leagueProfile,
        CancellationToken ct)
    {
        var teamList = teams.ToList();
        if (teamList.Count == 0) return;

        var league = leagueProfile.League;
        var teamNames = teamList.Select(t => t.Team).ToHashSet();
        var existingTeams = await db.TeamStrengths
            .Where(t => t.League == league && teamNames.Contains(t.Team))
            .ToDictionaryAsync(t => t.Team, ct);

        foreach (var t in teamList)
        {
            if (existingTeams.TryGetValue(t.Team, out var current))
            {
                db.Entry(current).CurrentValues.SetValues(new
                {
                    t.Attack,
                    t.Defence,
                    t.SampleSize,
                    FittedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                db.TeamStrengths.Add(t);
            }
        }

        var existingProfile = await db.LeagueStrengthProfiles
            .FirstOrDefaultAsync(p => p.League == league, ct);
        if (existingProfile is null)
        {
            db.LeagueStrengthProfiles.Add(leagueProfile);
        }
        else
        {
            db.Entry(existingProfile).CurrentValues.SetValues(new
            {
                leagueProfile.AvgHomeGoals,
                leagueProfile.AvgAwayGoals,
                leagueProfile.HomeAdvantage,
                leagueProfile.DixonColesRho,
                leagueProfile.SampleSize,
                FittedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
