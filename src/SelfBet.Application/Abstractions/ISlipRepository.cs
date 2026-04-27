using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface ISlipRepository
{
    Task SaveAsync(IReadOnlyCollection<Slip> slips, CancellationToken cancellationToken);
    Task UpdateAsync(Slip slip, CancellationToken cancellationToken);
    Task<Slip?> GetByIdAsync(Guid slipId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Slip>> GetByRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Slip>> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<Slip>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}
