using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for scheduling notifications for future delivery.
/// </summary>
public class ScheduleNotificationRequestValidator : AbstractValidator<ScheduleNotificationRequest>
{
    public ScheduleNotificationRequestValidator()
    {
        // Include all base notification validation
        Include(new SendNotificationRequestValidator());

        // ScheduledFor must be in the future
        RuleFor(x => x.ScheduledFor)
            .NotEmpty()
            .WithMessage("Scheduled time is required")
            .Must(BeInTheFuture)
            .WithMessage("Scheduled time must be in the future")
            .Must(BeWithinOneYear)
            .WithMessage("Scheduled time must be within one year from now");
    }

    private bool BeInTheFuture(DateTime scheduledFor)
    {
        return scheduledFor > DateTime.UtcNow;
    }

    private bool BeWithinOneYear(DateTime scheduledFor)
    {
        return scheduledFor <= DateTime.UtcNow.AddYears(1);
    }
}
