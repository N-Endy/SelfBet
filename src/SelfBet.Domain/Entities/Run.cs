using SelfBet.Domain.Enums;

namespace SelfBet.Domain.Entities;

public sealed class Run
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Planned;
    public string Trigger { get; init; } = "manual";
    public string? Message { get; set; }
    public int FixturesEvaluated { get; set; }
    public int CandidatesGenerated { get; set; }
    public int SlipsBuilt { get; set; }
}
