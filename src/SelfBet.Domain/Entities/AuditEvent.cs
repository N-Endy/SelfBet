namespace SelfBet.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string EventType { get; init; }
    public required string Message { get; init; }
    public string? MetadataJson { get; init; }
}
