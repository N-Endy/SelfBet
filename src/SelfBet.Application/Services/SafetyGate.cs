using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.Services;

public sealed class SafetyGate : ISafetyGate
{
    public SafetyGateOutcome Evaluate(RunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.Slips.Count == 0)
        {
            return SafetyGateOutcome.Block;
        }

        var failed = outcome.Slips.Count(s => s.Status == SlipStatus.Failed);
        if (failed == outcome.Slips.Count)
        {
            return SafetyGateOutcome.Block;
        }

        if (failed > 0)
        {
            return SafetyGateOutcome.HoldForConfirmation;
        }

        var oddsOutOfRange = outcome.Slips.Any(s => s.TotalOdds <= 0);
        if (oddsOutOfRange)
        {
            return SafetyGateOutcome.HoldForConfirmation;
        }

        return SafetyGateOutcome.Pass;
    }
}
