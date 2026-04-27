using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IAutomationGateway
{
    Task<PlacementAttempt> PlaceSlipAsync(Slip slip, CancellationToken cancellationToken);
    Task<decimal?> ReadAccountBalanceAsync(CancellationToken cancellationToken);
}
