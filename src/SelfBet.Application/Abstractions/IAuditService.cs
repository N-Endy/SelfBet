using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IAuditService
{
    Task LogAsync(string eventType, string message, object? metadata, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditEvent>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}
