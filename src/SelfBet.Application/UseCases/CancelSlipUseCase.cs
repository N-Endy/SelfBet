using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.UseCases;

public sealed class CancelSlipUseCase(
    ISlipRepository slipRepository,
    IAuditService auditService)
{
    public async Task<SlipCommandResult> ExecuteAsync(Guid slipId, CancellationToken cancellationToken)
    {
        var slip = await slipRepository.GetByIdAsync(slipId, cancellationToken);
        if (slip is null)
        {
            return SlipCommandResult.Failed("Slip not found.");
        }

        if (slip.Status is SlipStatus.Placed or SlipStatus.Won
                or SlipStatus.Lost or SlipStatus.Void)
        {
            return SlipCommandResult.Failed(
                $"A slip in state {slip.Status} cannot be cancelled from the dashboard.");
        }

        if (slip.Status == SlipStatus.Cancelled)
        {
            return SlipCommandResult.Failed("This slip is already cancelled.");
        }

        slip.Status = SlipStatus.Cancelled;
        await slipRepository.UpdateAsync(slip, cancellationToken);
        await auditService.LogAsync("slip.cancelled", "Slip cancelled", new { slip.Id }, cancellationToken);
        return SlipCommandResult.Success("Slip cancelled.");
    }
}
