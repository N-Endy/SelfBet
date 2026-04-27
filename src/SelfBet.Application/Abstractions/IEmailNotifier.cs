using SelfBet.Application.Models;

namespace SelfBet.Application.Abstractions;

public interface IEmailNotifier
{
    Task SendRunSummaryAsync(RunSummaryEmail summary, CancellationToken ct = default);
}
