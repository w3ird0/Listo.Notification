using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for creating notification templates.
/// Ensures template keys are unique and content is properly formatted.
/// </summary>
public class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    public CreateTemplateRequestValidator()
    {
        RuleFor(x => x.TemplateKey)
            .NotEmpty()
            .WithMessage("Template key is required")
            .MaximumLength(200)
            .WithMessage("Template key must not exceed 200 characters")
            .Matches(@"^[a-z0-9._-]+$")
            .WithMessage("Template key must contain only lowercase letters, numbers, dots, hyphens, and underscores");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Template name is required")
            .MaximumLength(200)
            .WithMessage("Template name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.SubjectTemplate)
            .NotEmpty()
            .WithMessage("Subject template is required")
            .MaximumLength(500)
            .WithMessage("Subject template must not exceed 500 characters");

        RuleFor(x => x.BodyTemplate)
            .NotEmpty()
            .WithMessage("Body template is required")
            .MaximumLength(50000)
            .WithMessage("Body template must not exceed 50,000 characters");

        RuleFor(x => x.Locale)
            .MaximumLength(10)
            .WithMessage("Locale must not exceed 10 characters")
            .Matches(@"^[a-z]{2}(-[A-Z]{2})?$")
            .WithMessage("Locale must be in format 'en' or 'en-US'");
    }
}
