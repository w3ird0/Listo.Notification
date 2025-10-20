using FluentValidation;
using Listo.Notification.Application.DTOs;
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.Validators;

public class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.Recipient)
            .NotEmpty()
            .WithMessage("Recipient is required");

        RuleFor(x => x.Recipient)
            .Must(BeValidEmail)
            .When(x => x.Channel == NotificationChannel.Email)
            .WithMessage("Invalid email address");

        RuleFor(x => x.Recipient)
            .Must(BeValidPhoneNumber)
            .When(x => x.Channel == NotificationChannel.Sms)
            .WithMessage("Invalid phone number format. Use E.164 format (e.g., +1234567890)");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .When(x => x.Channel == NotificationChannel.Email)
            .WithMessage("Subject is required for email notifications");

        RuleFor(x => x.Body)
            .NotEmpty()
            .WithMessage("Body is required")
            .MaximumLength(10000)
            .WithMessage("Body cannot exceed 10000 characters");

        RuleFor(x => x.ScheduledFor)
            .Must(BeInFuture)
            .When(x => x.ScheduledFor.HasValue)
            .WithMessage("Scheduled time must be in the future");
    }

    private bool BeValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private bool BeValidPhoneNumber(string phoneNumber)
    {
        // E.164 format validation: +[country code][number]
        return System.Text.RegularExpressions.Regex.IsMatch(
            phoneNumber, 
            @"^\+[1-9]\d{1,14}$");
    }

    private bool BeInFuture(DateTime? dateTime)
    {
        return dateTime > DateTime.UtcNow;
    }
}
