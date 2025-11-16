using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for update notification requests (PATCH operations).
/// Only validates fields that are being updated (all optional).
/// </summary>
public class UpdateNotificationRequestValidator : AbstractValidator<UpdateNotificationRequest>
{
    public UpdateNotificationRequestValidator()
    {
        // Status validation (if provided)
        When(x => !string.IsNullOrWhiteSpace(x.Status), () =>
        {
            RuleFor(x => x.Status)
                .Must(status => status == "Cancelled")
                .WithMessage("Only 'Cancelled' status update is allowed via API");
        });

        // CancellationReason required when status is Cancelled
        When(x => x.Status == "Cancelled", () =>
        {
            RuleFor(x => x.CancellationReason)
                .NotEmpty()
                .WithMessage("Cancellation reason is required when cancelling a notification")
                .MaximumLength(500)
                .WithMessage("Cancellation reason must not exceed 500 characters");
        });
    }
}
