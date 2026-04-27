using SelfBet.Domain.Entities;

namespace SelfBet.Application.Models;

public sealed class SlipBuildResult
{
    public required IReadOnlyList<Slip> Slips { get; init; }
    public required IReadOnlyList<CandidateBet> Candidates { get; init; }
    public string? Notes { get; init; }
}
