namespace SelfBet.Application.Services;

/// <summary>
/// Canonical market/outcome names used across prediction, storage, and SportyBet placement.
/// </summary>
public static class MarketOutcomeNormalizer
{
    public static string NormalizeOutcome(string market, string outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome)) return outcome;

        var m = market.Trim();
        var o = outcome.Trim();

        if (m.Equals("DoubleChance", StringComparison.OrdinalIgnoreCase))
        {
            return o.ToUpperInvariant() switch
            {
                "HOMEORDRAW" or "HOMEDRAW" or "1" => "1X",
                "DRAWORAWAY" or "DRAWAWAY" or "2" => "X2",
                "HOMEORAWAY" or "HOMEAWAY" or "12" => "12",
                _ => o
            };
        }

        if (m.Equals("1X2", StringComparison.OrdinalIgnoreCase))
        {
            return o.ToUpperInvariant() switch
            {
                "1" or "HOME" => "Home",
                "X" or "DRAW" => "Draw",
                "2" or "AWAY" => "Away",
                _ => o
            };
        }

        if (m.Equals("BTTS", StringComparison.OrdinalIgnoreCase))
        {
            return o.ToUpperInvariant() switch
            {
                "Y" or "YES" => "Yes",
                "N" or "NO" => "No",
                _ => o
            };
        }

        return o;
    }

    public static string NormalizeMarket(string market) =>
        market.Trim() switch
        {
            var m when m.Equals("OVER 2.5", StringComparison.OrdinalIgnoreCase) => "Over2.5",
            var m when m.Equals("UNDER 2.5", StringComparison.OrdinalIgnoreCase) => "Under2.5",
            _ => market.Trim()
        };
}
