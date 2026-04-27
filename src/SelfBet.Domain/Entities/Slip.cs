using SelfBet.Domain.Enums;

namespace SelfBet.Domain.Entities;

public sealed class Slip
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RunId { get; init; }
    public DateOnly RunDate { get; init; }
    public int Sequence { get; init; }
    public decimal Stake { get; set; }
    public decimal TotalOdds { get; set; } = 1m;
    public decimal PotentialReturn => Math.Round(Stake * TotalOdds, 2);
    public decimal Payout { get; set; }
    public SlipStatus Status { get; set; } = SlipStatus.Planned;
    public string? FailureReason { get; set; }

    /// <summary>SportyBet booking share code (e.g. "AB12C"). Used to load the slip into the app.</summary>
    public string? BookingCode { get; set; }

    /// <summary>Full share URL returned by SportyBet (deep-links into the Android app).</summary>
    public string? BookingUrl { get; set; }

    public string? ExternalTicketId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PlacedAtUtc { get; set; }
    public DateTimeOffset? SettledAtUtc { get; set; }
    public List<SlipLeg> Legs { get; init; } = [];
}
