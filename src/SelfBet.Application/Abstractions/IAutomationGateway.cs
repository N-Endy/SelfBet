using SelfBet.Application.Models;
using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IAutomationGateway
{
    Task<PlacementAttempt> PlaceSlipAsync(
        Slip slip,
        CancellationToken cancellationToken,
        IReadOnlyList<SportyBetPlacementFixture>? placementFixtures = null);

    Task<decimal?> ReadAccountBalanceAsync(CancellationToken cancellationToken);
}
