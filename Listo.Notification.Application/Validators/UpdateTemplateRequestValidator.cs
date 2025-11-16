using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for updating notification templates (PUT operations).
/// Similar to create but allows partial updates.
/// </summary>
public class UpdateTemplateRequestValidator : AbstractValidator<UpdateTemplateRequest>
{
    public UpdateTemplateRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("Template name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description))
            .WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.SubjectTemplate)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.SubjectTemplate))
            .WithMessage("Subject template must not exceed 500 characters");

        RuleFor(x => x.BodyTemplate)
            .MaximumLength(50000)
            .When(x => !string.IsNullOrWhiteSpace(x.BodyTemplate))
            .WithMessage("Body template must not exceed 50,000 characters");
    }
}
