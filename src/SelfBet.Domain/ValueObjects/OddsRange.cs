namespace SelfBet.Domain.ValueObjects;

public readonly record struct OddsRange(decimal Min, decimal Max)
{
    public bool Contains(decimal value) => value >= Min && value <= Max;

    public static OddsRange Default => new(6m, 10m);
}
