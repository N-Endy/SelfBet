using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Entities;
using SelfBet.Infrastructure.Persistence;

namespace SelfBet.Infrastructure.Services;

public sealed class DbAuditService(SelfBetDbContext db) : IAuditService
{
    public async Task LogAsync(string eventType, string message, object? metadata, CancellationToken ct = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = eventType,
            Message = message,
            MetadataJson = metadata is not null ? JsonSerializer.Serialize(metadata) : null
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEvent>> GetRecentAsync(int limit, CancellationToken ct = default) =>
        await db.AuditEvents
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(limit)
            .ToListAsync(ct);
}
