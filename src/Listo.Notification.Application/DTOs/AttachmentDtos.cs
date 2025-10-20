using Microsoft.AspNetCore.Http;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to upload an attachment.
/// </summary>
public record UploadAttachmentRequest
{
    public required IFormFile File { get; init; }
    public string? Description { get; init; }
    public AttachmentType Type { get; init; } = AttachmentType.General;
}

/// <summary>
/// Response after successful upload.
/// </summary>
public record AttachmentResponse
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required string BlobUrl { get; init; }
    public string? Description { get; init; }
    public required AttachmentType Type { get; init; }
    public required DateTime UploadedAt { get; init; }
    public required Guid UploadedBy { get; init; }
}

/// <summary>
/// Attachment type enumeration.
/// </summary>
public enum AttachmentType
{
    General,
    EmailAttachment,
    TemplateFile,
    InAppMessageAttachment
}
