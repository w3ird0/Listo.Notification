using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for batch internal notification requests.
/// </summary>
public class BatchInternalNotificationRequestValidator : AbstractValidator<BatchInternalNotificationRequest>
{
    public BatchInternalNotificationRequestValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required");

        RuleFor(x => x.Notifications)
            .NotNull()
            .WithMessage("Notifications list is required")
            .Must(n => n != null && n.Any())
            .WithMessage("Notifications list cannot be empty")
            .Must(n => n == null || n.Count() <= 100)
            .WithMessage("Batch size cannot exceed 100 notifications");

        RuleForEach(x => x.Notifications)
            .SetValidator(new InternalNotificationRequestValidator());
    }
}
