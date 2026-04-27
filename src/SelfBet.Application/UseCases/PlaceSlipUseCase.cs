using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.UseCases;

public sealed class PlaceSlipUseCase(
    ISlipRepository slipRepository,
    IPlacementRepository placementRepository,
    IAutomationGateway automationGateway,
    IAuditService auditService,
    ILogger<PlaceSlipUseCase> logger)
{
    public async Task<bool> ExecuteAsync(Guid slipId, CancellationToken cancellationToken)
    {
        var slip = await slipRepository.GetByIdAsync(slipId, cancellationToken);
        if (slip is null)
        {
            return false;
        }

        if (slip.Status is not (SlipStatus.Ready or SlipStatus.AwaitingConfirmation))
        {
            logger.LogWarning("Slip {SlipId} is not in a placeable state ({Status})", slipId, slip.Status);
            return false;
        }

        var attempt = await automationGateway.PlaceSlipAsync(slip, cancellationToken);
        await placementRepository.SaveAsync(attempt, cancellationToken);

        if (attempt.Success)
        {
            slip.Status = SlipStatus.Placed;
            slip.PlacedAtUtc = attempt.AttemptedAtUtc;
            slip.ExternalTicketId = attempt.ExternalTicketId;
        }
        else
        {
            slip.Status = SlipStatus.Failed;
            slip.FailureReason = attempt.Error;
        }

        await slipRepository.UpdateAsync(slip, cancellationToken);
        await auditService.LogAsync(
            attempt.Success ? "slip.placed" : "slip.failed",
            attempt.Error ?? "Slip placed",
            new { slip.Id, slip.ExternalTicketId, slip.Status },
            cancellationToken);

        return attempt.Success;
    }
}
