using SelfBet.Application.Models;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.Abstractions;

public interface ISafetyGate
{
    SafetyGateOutcome Evaluate(RunOutcome outcome);
}
