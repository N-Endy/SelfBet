namespace SelfBet.Domain.Entities;

public sealed class PlacementAttempt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SlipId { get; init; }
    public DateTimeOffset AttemptedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool Success { get; init; }
    public string? ExternalTicketId { get; init; }
    public string? BookingCode { get; init; }
    public string? BookingUrl { get; init; }
    public string? EvidencePath { get; init; }
    public string? Error { get; init; }
    /// <summary>booking_code | full_auth | dry_run</summary>
    public string PlacementMode { get; init; } = "booking_code";
}
