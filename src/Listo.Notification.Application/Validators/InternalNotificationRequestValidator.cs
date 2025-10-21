using FluentValidation;
using Listo.Notification.Application.DTOs;
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for internal service-to-service notification requests.
/// Supports template-based flow and synchronous delivery.
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

        // Template-based flow validation
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.TemplateKey) || !string.IsNullOrEmpty(x.Body))
            .WithMessage("Either TemplateKey or Body must be provided");

        RuleFor(x => x.Variables)
            .NotNull()
            .WithMessage("Variables are required when using TemplateKey")
            .When(x => !string.IsNullOrEmpty(x.TemplateKey));

        RuleFor(x => x.Locale)
            .MaximumLength(10)
            .WithMessage("Locale cannot exceed 10 characters")
            .When(x => x.Locale != null);

        // Synchronous delivery validation
        RuleFor(x => x.Channel)
            .Must(c => c != NotificationChannel.InApp)
            .WithMessage("Synchronous delivery not supported for In-App notifications")
            .When(x => x.Synchronous);

        // Warning for non-SMS synchronous (not a validation failure, just logging)
        RuleFor(x => x)
            .Custom((request, context) =>
            {
                if (request.Synchronous && request.Channel != NotificationChannel.Sms)
                {
                    var failure = new FluentValidation.Results.ValidationFailure(
                        nameof(request.Synchronous),
                        "Synchronous delivery is recommended for SMS only (critical use cases)")
                    {
                        Severity = Severity.Warning
                    };
                    context.AddFailure(failure);
                }
            });
    }
}
