using SelfBet.Domain.Entities;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.Models;

public sealed class RunOutcome
{
    public required Run Run { get; init; }
    public required IReadOnlyList<Slip> Slips { get; init; }
    public RunStatus Status => Run.Status;
    public string? Message => Run.Message;
}
