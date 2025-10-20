using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for internal service-to-service notification requests.
/// Requires ServiceName for tracking and correlation.
/// </summary>
public class InternalNotificationRequestValidator : AbstractValidator<InternalNotificationRequest>
{
    public InternalNotificationRequestValidator()
    {
        // Include all base notification validation
        Include(new SendNotificationRequestValidator());

        // ServiceName is required for internal requests
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required for internal requests")
            .MaximumLength(100)
            .WithMessage("Service name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z0-9._-]+$")
            .WithMessage("Service name must contain only letters, numbers, dots, hyphens, and underscores");

        // EventType for CloudEvents pattern
        RuleFor(x => x.EventType)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.EventType))
            .WithMessage("Event type must not exceed 200 characters");
    }
}
