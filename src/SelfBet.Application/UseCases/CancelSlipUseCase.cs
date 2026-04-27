using SelfBet.Application.Abstractions;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.UseCases;

public sealed class CancelSlipUseCase(
    ISlipRepository slipRepository,
    IAuditService auditService)
{
    public async Task<bool> ExecuteAsync(Guid slipId, CancellationToken cancellationToken)
    {
        var slip = await slipRepository.GetByIdAsync(slipId, cancellationToken);
        if (slip is null)
        {
            return false;
        }

        if (slip.Status is SlipStatus.Placed or SlipStatus.Won
                or SlipStatus.Lost or SlipStatus.Void)
        {
            return false;
        }

        slip.Status = SlipStatus.Cancelled;
        await slipRepository.UpdateAsync(slip, cancellationToken);
        await auditService.LogAsync("slip.cancelled", "Slip cancelled", new { slip.Id }, cancellationToken);
        return true;
    }
}
