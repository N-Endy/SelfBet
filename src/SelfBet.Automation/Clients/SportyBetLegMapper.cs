using System.Text.RegularExpressions;
using SelfBet.Application.Models;
using SelfBet.Application.Services;
using SelfBet.Domain.Entities;

namespace SelfBet.Automation.Clients;

public static class SportyBetLegMapper
{
    private static readonly Regex NonWordRegex = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly HashSet<string> NoiseWords =
    [
        "fc", "cf", "sc", "afc", "club", "the", "de", "da", "do", "cd", "ud", "ac", "as",
        "fk", "sk", "nk", "if", "bk", "city", "united", "athletic"
    ];

    public static SportyBetPlacementFixture? FindBestFixture(
        IReadOnlyList<SportyBetPlacementFixture> fixtures,
        SlipLeg leg)
    {
        var parts = leg.MatchTitle.Split(" vs ", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return null;

        var scored = fixtures
            .Select(f => new
            {
                Fixture = f,
                Score = TeamSimilarity(parts[0], f.HomeTeam) + TeamSimilarity(parts[1], f.AwayTeam)
            })
            .Where(x => x.Score >= 1.30)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return scored?.Fixture;
    }

    public static SportyBetSelectionDto? MapLegToSelection(SportyBetPlacementFixture fixture, string market, string outcome)
    {
        var normalizedMarket = MarketOutcomeNormalizer.NormalizeMarket(market);
        var normalizedOutcome = MarketOutcomeNormalizer.NormalizeOutcome(normalizedMarket, outcome);

        var (marketId, outcomeId, specifier) = (normalizedMarket.ToUpperInvariant(), normalizedOutcome.ToUpperInvariant()) switch
        {
            ("1X2", "HOME") => ("1", fixture.HomeOutcomeId, ""),
            ("1X2", "DRAW") => ("1", fixture.DrawOutcomeId, ""),
            ("1X2", "AWAY") => ("1", fixture.AwayOutcomeId, ""),
            ("OVER2.5", "OVER2.5") => ("18", fixture.Over25OutcomeId, "total=2.5"),
            ("UNDER2.5", "UNDER2.5") => ("18", fixture.Under25OutcomeId, "total=2.5"),
            ("BTTS", "YES") => ("29", fixture.BttsYesOutcomeId, ""),
            ("BTTS", "NO") => ("29", fixture.BttsNoOutcomeId, ""),
            ("DRAWNOBET", "HOME") => ("1", fixture.HomeOutcomeId, ""),
            ("DRAWNOBET", "AWAY") => ("1", fixture.AwayOutcomeId, ""),
            ("DOUBLECHANCE", "1X") => DcSelection(fixture, fixture.Dc1XOutcomeId, fixture.HomeOutcomeId),
            ("DOUBLECHANCE", "X2") => DcSelection(fixture, fixture.DcX2OutcomeId, fixture.AwayOutcomeId),
            ("DOUBLECHANCE", "12") => DcSelection(fixture, fixture.Dc12OutcomeId, fixture.HomeOutcomeId),
            _ => ("", "", "")
        };

        if (string.IsNullOrEmpty(marketId) || string.IsNullOrEmpty(outcomeId)) return null;

        return new SportyBetSelectionDto
        {
            EventId = fixture.EventId,
            MarketId = marketId,
            OutcomeId = outcomeId,
            Specifier = specifier
        };
    }

    private static (string MarketId, string OutcomeId, string Specifier) DcSelection(
        SportyBetPlacementFixture fixture,
        string dcOutcomeId,
        string fallback1X2OutcomeId)
    {
        if (!string.IsNullOrEmpty(dcOutcomeId))
            return ("10", dcOutcomeId, "");
        return ("1", fallback1X2OutcomeId, "");
    }

    private static double TeamSimilarity(string expected, string actual)
    {
        var e = Normalize(expected);
        var a = Normalize(actual);
        if (e == a) return 1.0;
        var et = Tokenize(e);
        var at = Tokenize(a);
        if (et.Count == 0 || at.Count == 0) return 0;
        var overlap = et.Intersect(at).Count();
        var shorter = Math.Min(et.Count, at.Count);
        var ratio = shorter == 0 ? 0 : overlap / (double)shorter;
        if (e.Replace(" ", "") == a.Replace(" ", "")) ratio = Math.Max(ratio, 0.9);
        if (et.FirstOrDefault() == at.FirstOrDefault() && overlap > 0) ratio = Math.Max(ratio, 0.78);
        return Math.Min(1.0, ratio);
    }

    private static string Normalize(string v) =>
        string.Join(" ", NonWordRegex.Replace(v.ToLowerInvariant(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static List<string> Tokenize(string v) =>
        v.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !NoiseWords.Contains(w)).ToList();
}
