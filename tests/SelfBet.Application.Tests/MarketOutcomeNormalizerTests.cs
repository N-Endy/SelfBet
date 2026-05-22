using SelfBet.Application.Services;
using Xunit;

namespace SelfBet.Application.Tests;

public sealed class MarketOutcomeNormalizerTests
{
    [Theory]
    [InlineData("HomeOrDraw", "1X")]
    [InlineData("DrawOrAway", "X2")]
    [InlineData("HOME", "Home")]
    public void NormalizeOutcome_maps_aliases(string input, string expected)
    {
        var market = input.Contains("Draw") && input != "HOME" ? "DoubleChance" : input == "HOME" ? "1X2" : "DoubleChance";
        if (input == "HOME") market = "1X2";

        var result = MarketOutcomeNormalizer.NormalizeOutcome(market, input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeOutcome_doubleChance_homeOrDraw()
    {
        Assert.Equal("1X", MarketOutcomeNormalizer.NormalizeOutcome("DoubleChance", "HomeOrDraw"));
        Assert.Equal("X2", MarketOutcomeNormalizer.NormalizeOutcome("DoubleChance", "DrawOrAway"));
        Assert.Equal("12", MarketOutcomeNormalizer.NormalizeOutcome("DoubleChance", "12"));
    }
}
