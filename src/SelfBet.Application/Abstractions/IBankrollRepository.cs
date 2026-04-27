using SelfBet.Domain.Entities;

namespace SelfBet.Application.Abstractions;

public interface IBankrollRepository
{
    Task<BankrollSnapshot?> GetLatestAsync(CancellationToken cancellationToken);
    Task SaveAsync(BankrollSnapshot snapshot, CancellationToken cancellationToken);
    Task<IReadOnlyList<BankrollSnapshot>> GetHistoryAsync(int limit, CancellationToken cancellationToken);
}
