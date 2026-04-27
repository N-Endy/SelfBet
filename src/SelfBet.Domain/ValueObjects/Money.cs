namespace SelfBet.Domain.ValueObjects;

public readonly record struct Money(decimal Amount, string Currency = "NGN")
{
    public static Money Zero(string currency = "NGN") => new(0m, currency);

    public Money RoundDownTo(decimal increment)
    {
        if (increment <= 0)
        {
            return this;
        }

        var rounded = Math.Floor(Amount / increment) * increment;
        return this with { Amount = rounded };
    }
}
