using Microsoft.Extensions.Options;
using SelfBet.Infrastructure.Providers;

namespace SelfBet.Infrastructure.Services;

/// <summary>
/// Maps SportyBet league display names to canonical names used in team-strength / historical data.
/// </summary>
public sealed class LeagueNameResolver(IOptions<ApiFootballOptions> options)
{
    private readonly Dictionary<string, string> _aliases = BuildAliasMap(options.Value.LeagueIdMap);

    public string Resolve(string league)
    {
        if (string.IsNullOrWhiteSpace(league)) return league;

        if (_aliases.TryGetValue(Normalize(league), out var canonical))
            return canonical;

        foreach (var (alias, resolved) in _aliases)
        {
            if (Normalize(league).Contains(alias, StringComparison.Ordinal) ||
                alias.Contains(Normalize(league), StringComparison.Ordinal))
                return resolved;
        }

        return league;
    }

    private static Dictionary<string, string> BuildAliasMap(Dictionary<string, int> leagueIdMap)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in leagueIdMap.Keys)
        {
            var norm = Normalize(key);
            map[norm] = key;
            map[norm.Replace(" ", "")] = key;
        }

        map[Normalize("Spain - LaLiga")] = "Spain - LaLiga";
        map[Normalize("Spain - La Liga")] = "Spain - LaLiga";
        return map;
    }

    private static string Normalize(string s) =>
        string.Join(" ", s.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
