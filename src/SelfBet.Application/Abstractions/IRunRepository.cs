using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IRunRepository
{
    Task SaveAsync(Run run, CancellationToken cancellationToken);
    Task<Run?> GetByIdAsync(Guid runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Run>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}
