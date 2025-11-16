using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

/// <summary>
/// Validator for creating user notification preferences.
/// </summary>
public class CreatePreferencesRequestValidator : AbstractValidator<CreatePreferencesRequest>
{
    public CreatePreferencesRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required")
            .MaximumLength(100)
            .WithMessage("User ID must not exceed 100 characters");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required")
            .MaximumLength(100)
            .WithMessage("Tenant ID must not exceed 100 characters");

        RuleFor(x => x.Language)
            .MaximumLength(10)
            .When(x => !string.IsNullOrWhiteSpace(x.Language))
            .WithMessage("Language must not exceed 10 characters")
            .Matches(@"^[a-z]{2}(-[A-Z]{2})?$")
            .When(x => !string.IsNullOrWhiteSpace(x.Language))
            .WithMessage("Language must be in format 'en' or 'en-US'");

        RuleFor(x => x.Timezone)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Timezone))
            .WithMessage("Timezone must not exceed 100 characters");

        // Quiet hours validation
        When(x => x.QuietHoursStart.HasValue, () =>
        {
            RuleFor(x => x.QuietHoursStart)
                .Must(BeValidTimeSpan)
                .WithMessage("Quiet hours start must be between 00:00:00 and 23:59:59");
        });

        When(x => x.QuietHoursEnd.HasValue, () =>
        {
            RuleFor(x => x.QuietHoursEnd)
                .Must(BeValidTimeSpan)
                .WithMessage("Quiet hours end must be between 00:00:00 and 23:59:59");
        });
    }

    private bool BeValidTimeSpan(TimeSpan? timeSpan)
    {
        if (!timeSpan.HasValue) return true;
        return timeSpan.Value >= TimeSpan.Zero && timeSpan.Value < TimeSpan.FromDays(1);
    }
}

/// <summary>
/// Validator for updating user notification preferences (PATCH operations).
/// </summary>
public class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
{
    public UpdatePreferencesRequestValidator()
    {
        RuleFor(x => x.Language)
            .MaximumLength(10)
            .When(x => !string.IsNullOrWhiteSpace(x.Language))
            .WithMessage("Language must not exceed 10 characters")
            .Matches(@"^[a-z]{2}(-[A-Z]{2})?$")
            .When(x => !string.IsNullOrWhiteSpace(x.Language))
            .WithMessage("Language must be in format 'en' or 'en-US'");

        RuleFor(x => x.Timezone)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Timezone))
            .WithMessage("Timezone must not exceed 100 characters");

        // Quiet hours validation
        When(x => x.QuietHoursStart.HasValue, () =>
        {
            RuleFor(x => x.QuietHoursStart)
                .Must(BeValidTimeSpan)
                .WithMessage("Quiet hours start must be between 00:00:00 and 23:59:59");
        });

        When(x => x.QuietHoursEnd.HasValue, () =>
        {
            RuleFor(x => x.QuietHoursEnd)
                .Must(BeValidTimeSpan)
                .WithMessage("Quiet hours end must be between 00:00:00 and 23:59:59");
        });
    }

    private bool BeValidTimeSpan(TimeSpan? timeSpan)
    {
        if (!timeSpan.HasValue) return true;
        return timeSpan.Value >= TimeSpan.Zero && timeSpan.Value < TimeSpan.FromDays(1);
    }
}
