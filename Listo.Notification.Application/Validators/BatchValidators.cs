using FluentValidation;
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Validators;

public class BatchSendRequestValidator : AbstractValidator<BatchSendRequest>
{
    public BatchSendRequestValidator()
    {
        RuleFor(x => x.Notifications)
            .NotEmpty()
            .WithMessage("At least one notification is required")
            .Must(list => list.Count <= 1000)
            .WithMessage("Batch size cannot exceed 1000 notifications");

        RuleForEach(x => x.Notifications)
            .SetValidator(new BatchNotificationItemValidator());
    }
}

public class BatchNotificationItemValidator : AbstractValidator<BatchNotificationItem>
{
    public BatchNotificationItemValidator()
    {
        RuleFor(x => x.Recipient)
            .NotEmpty()
            .WithMessage("Recipient is required");

        RuleFor(x => x.TemplateKey)
            .NotEmpty()
            .WithMessage("Template key is required");

        RuleFor(x => x.Variables)
            .NotNull()
            .WithMessage("Variables dictionary is required");
    }
}

public class BatchScheduleRequestValidator : AbstractValidator<BatchScheduleRequest>
{
    public BatchScheduleRequestValidator()
    {
        RuleFor(x => x.Notifications)
            .NotEmpty()
            .WithMessage("At least one notification is required")
            .Must(list => list.Count <= 1000)
            .WithMessage("Batch size cannot exceed 1000 notifications");

        RuleForEach(x => x.Notifications)
            .SetValidator(new BatchScheduleItemValidator());
    }
}

public class BatchScheduleItemValidator : AbstractValidator<BatchScheduleItem>
{
    public BatchScheduleItemValidator()
    {
        RuleFor(x => x.Recipient)
            .NotEmpty()
            .WithMessage("Recipient is required");

        RuleFor(x => x.TemplateKey)
            .NotEmpty()
            .WithMessage("Template key is required");

        RuleFor(x => x.ScheduledFor)
            .Must(BeInFuture)
            .WithMessage("Scheduled time must be in the future");

        RuleFor(x => x.Variables)
            .NotNull()
            .WithMessage("Variables dictionary is required");
    }

    private bool BeInFuture(DateTime dateTime)
    {
        return dateTime > DateTime.UtcNow;
    }
}
