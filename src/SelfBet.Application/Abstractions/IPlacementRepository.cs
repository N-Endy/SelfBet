using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IPlacementRepository
{
    Task SaveAsync(PlacementAttempt attempt, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlacementAttempt>> GetBySlipAsync(Guid slipId, CancellationToken cancellationToken);
}
