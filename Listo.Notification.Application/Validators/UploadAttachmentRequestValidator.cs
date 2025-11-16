using FluentValidation;
using Listo.Notification.Application.DTOs;
using Microsoft.Extensions.Configuration;

namespace Listo.Notification.Application.Validators;

public class UploadAttachmentRequestValidator : AbstractValidator<UploadAttachmentRequest>
{
    private readonly IConfiguration _configuration;

    public UploadAttachmentRequestValidator(IConfiguration configuration)
    {
        _configuration = configuration;

        var maxFileSizeBytes = _configuration.GetValue<long>("FileUpload:MaxFileSizeBytes", 10 * 1024 * 1024); // 10MB default
        var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>() 
            ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("File is required");

        RuleFor(x => x.File.Length)
            .LessThanOrEqualTo(maxFileSizeBytes)
            .When(x => x.File != null)
            .WithMessage($"File size cannot exceed {maxFileSizeBytes / (1024 * 1024)}MB");

        RuleFor(x => x.File.FileName)
            .Must(fileName => HasAllowedExtension(fileName, allowedExtensions))
            .When(x => x.File != null)
            .WithMessage($"File type not allowed. Allowed types: {string.Join(", ", allowedExtensions)}");
    }

    private bool HasAllowedExtension(string fileName, string[] allowedExtensions)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return allowedExtensions.Contains(extension);
    }
}
