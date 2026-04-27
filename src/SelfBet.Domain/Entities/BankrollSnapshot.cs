namespace SelfBet.Domain.Entities;

public sealed class BankrollSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public decimal Balance { get; init; }
    public decimal StakePerSlip { get; init; }
    public string Currency { get; init; } = "NGN";
    public string? Note { get; init; }
}
