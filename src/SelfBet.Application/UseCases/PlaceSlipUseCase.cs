using Microsoft.Extensions.Logging;
using SelfBet.Application.Abstractions;
using SelfBet.Application.Models;
using SelfBet.Domain.Enums;

namespace SelfBet.Application.UseCases;

public sealed class PlaceSlipUseCase(
    ISlipRepository slipRepository,
    IPlacementRepository placementRepository,
    IAutomationGateway automationGateway,
    IAuditService auditService,
    ILogger<PlaceSlipUseCase> logger)
{
    public async Task<SlipCommandResult> ExecuteAsync(Guid slipId, CancellationToken cancellationToken)
    {
        var slip = await slipRepository.GetByIdAsync(slipId, cancellationToken);
        if (slip is null)
        {
            return SlipCommandResult.Failed("Slip not found.");
        }

        if (slip.Status is not (SlipStatus.Ready or SlipStatus.AwaitingConfirmation))
        {
            logger.LogWarning("Slip {SlipId} is not in a placeable state ({Status})", slipId, slip.Status);
            return SlipCommandResult.Failed(
                $"This slip cannot be placed in its current state ({slip.Status}). Refresh the page.");
        }

        var attempt = await automationGateway.PlaceSlipAsync(slip, cancellationToken);
        await placementRepository.SaveAsync(attempt, cancellationToken);

        if (attempt.Success)
        {
            slip.BookingCode = attempt.BookingCode;
            slip.BookingUrl = attempt.BookingUrl;

            if (!string.IsNullOrEmpty(attempt.ExternalTicketId))
            {
                slip.Status = SlipStatus.Placed;
                slip.ExternalTicketId = attempt.ExternalTicketId;
                slip.PlacedAtUtc = attempt.AttemptedAtUtc;
            }
            else
            {
                // Booking / share code only — user still confirms in the app
                slip.Status = SlipStatus.AwaitingConfirmation;
            }
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

        return attempt.Success
            ? SlipCommandResult.Success(
                string.IsNullOrEmpty(attempt.ExternalTicketId)
                    ? "Booking code updated. Open the link or enter the code in SportyBet, then place the bet in the app."
                    : "Bet placed on SportyBet (full auth).")
            : SlipCommandResult.Failed(attempt.Error ?? "Placement failed.");
    }
}
