# Section 10-13: API, Validation, File Uploads Implementation Guide

## Overview

This document provides a complete implementation guide for Section 10-13, covering:
- RESTful API endpoints for all resources
- FluentValidation integration with comprehensive validators
- Azure Blob Storage for file uploads
- Admin operations for cost tracking and rate limit management
- Internal service-to-service endpoints
- Webhook handlers for provider callbacks

**Testing is deferred** as per requirements.

---

## Table of Contents

1. [DTOs (Data Transfer Objects)](#dtos-data-transfer-objects)
2. [FluentValidation Validators](#fluentvalidation-validators)
3. [Services & Repositories](#services--repositories)
4. [Controllers](#controllers)
5. [Azure Blob Storage Configuration](#azure-blob-storage-configuration)
6. [Registration in Program.cs](#registration-in-programcs)
7. [API Endpoints Documentation](#api-endpoints-documentation)

---

## DTOs (Data Transfer Objects)

### 1. Template DTOs (`TemplateDtos.cs`) - CREATED ✅

Located at: `src/Listo.Notification.Application/DTOs/TemplateDtos.cs`

Contains:
- `CreateTemplateRequest`
- `UpdateTemplateRequest`
- `TemplateResponse`
- `PagedTemplatesResponse`
- `RenderTemplateRequest`
- `RenderTemplateResponse`

### 2. Batch Operation DTOs (`BatchDtos.cs`)

```csharp
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to send notifications in batch.
/// </summary>
public record BatchSendRequest
{
    public required List<BatchNotificationItem> Notifications { get; init; }
    public bool ContinueOnError { get; init; } = true;
}

/// <summary>
/// Individual notification item in a batch.
/// </summary>
public record BatchNotificationItem
{
    public required NotificationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string TemplateKey { get; init; }
    public Dictionary<string, object>? Variables { get; init; }
    public Priority Priority { get; init; } = Priority.Normal;
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Response after batch send operation.
/// </summary>
public record BatchSendResponse
{
    public required Guid BatchId { get; init; }
    public required int TotalCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailureCount { get; init; }
    public required List<BatchItemResult> Results { get; init; }
    public required DateTime ProcessedAt { get; init; }
}

/// <summary>
/// Result for individual batch item.
/// </summary>
public record BatchItemResult
{
    public required int Index { get; init; }
    public Guid? NotificationId { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Request to schedule notifications in batch.
/// </summary>
public record BatchScheduleRequest
{
    public required List<BatchScheduleItem> Notifications { get; init; }
}

/// <summary>
/// Individual scheduled notification item.
/// </summary>
public record BatchScheduleItem
{
    public required NotificationChannel Channel { get; init; }
    public required string Recipient { get; init; }
    public required string TemplateKey { get; init; }
    public Dictionary<string, object>? Variables { get; init; }
    public required DateTime ScheduledFor { get; init; }
    public Priority Priority { get; init; } = Priority.Normal;
}
```

### 3. Attachment DTOs (`AttachmentDtos.cs`)

```csharp
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
```

### 4. Admin DTOs (`AdminDtos.cs`)

```csharp
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to update rate limit configuration.
/// </summary>
public record UpdateRateLimitRequest
{
    public required string Key { get; init; } // e.g., "user:{userId}", "service:{serviceOrigin}", "tenant:{tenantId}"
    public required int Limit { get; init; }
    public required int WindowSeconds { get; init; }
    public int? BurstCapacity { get; init; }
}

/// <summary>
/// Rate limit configuration response.
/// </summary>
public record RateLimitResponse
{
    public required string Key { get; init; }
    public required int Limit { get; init; }
    public required int WindowSeconds { get; init; }
    public int? BurstCapacity { get; init; }
    public required int CurrentUsage { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Cost tracking summary response.
/// </summary>
public record CostTrackingSummaryResponse
{
    public required Guid TenantId { get; init; }
    public required ServiceOrigin? ServiceOrigin { get; init; }
    public required decimal TotalCostUsd { get; init; }
    public required int NotificationCount { get; init; }
    public required Dictionary<NotificationChannel, decimal> CostByChannel { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
}

/// <summary>
/// Budget configuration request.
/// </summary>
public record UpdateBudgetRequest
{
    public required Guid TenantId { get; init; }
    public ServiceOrigin? ServiceOrigin { get; init; }
    public required decimal MonthlyBudgetUsd { get; init; }
    public bool EnableAlerts { get; init; } = true;
    public decimal AlertThreshold80Percent { get; init; } = 0.8m;
    public decimal AlertThreshold100Percent { get; init; } = 1.0m;
}
```

### 5. Internal/Service-to-Service DTOs (`InternalDtos.cs`)

```csharp
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Internal request to queue a notification for async processing.
/// </summary>
public record QueueNotificationRequest
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required ServiceOrigin ServiceOrigin { get; init; }
    public required NotificationChannel Channel { get; init; }
    public required string TemplateKey { get; init; }
    public required Dictionary<string, object> Variables { get; init; }
    public Priority Priority { get; init; } = Priority.Normal;
    public bool Synchronous { get; init; } = false;
    public DateTime? ScheduledFor { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Internal response after queueing.
/// </summary>
public record QueueNotificationResponse
{
    public required Guid QueueId { get; init; }
    public required string Status { get; init; }
    public required DateTime QueuedAt { get; init; }
}

/// <summary>
/// Health check response.
/// </summary>
public record HealthCheckResponse
{
    public required string Status { get; init; } // "Healthy", "Degraded", "Unhealthy"
    public required Dictionary<string, string> Components { get; init; }
    public required DateTime Timestamp { get; init; }
}
```

### 6. Webhook DTOs (`WebhookDtos.cs`)

```csharp
namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Twilio status callback payload.
/// </summary>
public record TwilioStatusCallbackRequest
{
    public required string MessageSid { get; init; }
    public required string MessageStatus { get; init; } // queued, sent, delivered, failed, undelivered
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required string To { get; init; }
    public required string From { get; init; }
    public DateTime? DateSent { get; init; }
}

/// <summary>
/// SendGrid event webhook payload.
/// </summary>
public record SendGridEventRequest
{
    public required string Email { get; init; }
    public required string Event { get; init; } // delivered, open, click, bounce, dropped, deferred, processed
    public required long Timestamp { get; init; }
    public string? SmtpId { get; init; }
    public string? Sg_event_id { get; init; }
    public string? Sg_message_id { get; init; }
    public string? Reason { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// FCM delivery status webhook payload.
/// </summary>
public record FcmDeliveryStatusRequest
{
    public required string MessageId { get; init; }
    public required string Status { get; init; } // sent, delivered, failed
    public string? DeviceToken { get; init; }
    public string? Error { get; init; }
    public required DateTime Timestamp { get; init; }
}
```

---

## FluentValidation Validators

### 1. SendNotificationRequestValidator

```csharp
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
```

### 2. CreateTemplateRequestValidator

```csharp
using FluentValidation;
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;

namespace Listo.Notification.Application.Validators;

public class CreateTemplateRequestValidator : AbstractValidator<CreateTemplateRequest>
{
    private readonly ITemplateRepository _templateRepository;

    public CreateTemplateRequestValidator(ITemplateRepository templateRepository)
    {
        _templateRepository = templateRepository;

        RuleFor(x => x.TemplateKey)
            .NotEmpty()
            .WithMessage("Template key is required")
            .Matches("^[a-z0-9_-]+$")
            .WithMessage("Template key must contain only lowercase letters, numbers, hyphens, and underscores")
            .MaximumLength(100)
            .WithMessage("Template key cannot exceed 100 characters")
            .MustAsync(BeUniqueTemplateKey)
            .WithMessage("Template key already exists");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.SubjectTemplate)
            .NotEmpty()
            .WithMessage("Subject template is required")
            .MaximumLength(500)
            .WithMessage("Subject cannot exceed 500 characters");

        RuleFor(x => x.BodyTemplate)
            .NotEmpty()
            .WithMessage("Body template is required")
            .MaximumLength(50000)
            .WithMessage("Body template cannot exceed 50000 characters");

        RuleFor(x => x.Locale)
            .Matches("^[a-z]{2}-[A-Z]{2}$")
            .WithMessage("Locale must be in format: xx-XX (e.g., en-US, fr-FR)");
    }

    private async Task<bool> BeUniqueTemplateKey(string templateKey, CancellationToken cancellationToken)
    {
        var existing = await _templateRepository.GetByKeyAsync(templateKey, cancellationToken);
        return existing == null;
    }
}
```

### 3. BatchSendRequestValidator

```csharp
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
```

### 4. UploadAttachmentRequestValidator

```csharp
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
```

---

## Services & Repositories

### 1. Template Service Interface

```csharp
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Interfaces;

public interface ITemplateService
{
    Task<TemplateResponse> CreateAsync(Guid tenantId, CreateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TemplateResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<TemplateResponse?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task<PagedTemplatesResponse> GetAllAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TemplateResponse?> UpdateAsync(Guid tenantId, Guid id, UpdateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<RenderTemplateResponse> RenderAsync(Guid tenantId, RenderTemplateRequest request, CancellationToken cancellationToken = default);
}
```

### 2. Attachment Service Interface

```csharp
using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Interfaces;

public interface IAttachmentService
{
    Task<AttachmentResponse> UploadAsync(Guid tenantId, Guid userId, UploadAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<Stream?> DownloadAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<AttachmentResponse?> GetMetadataAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken = default);
}
```

### 3. Admin Cost Tracking Service Interface

```csharp
using Listo.Notification.Application.DTOs;
using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.Interfaces;

public interface ICostTrackingService
{
    Task<CostTrackingSummaryResponse> GetSummaryAsync(
        Guid tenantId, 
        ServiceOrigin? serviceOrigin, 
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);
    
    Task<decimal> GetCurrentMonthSpendAsync(Guid tenantId, ServiceOrigin? serviceOrigin, CancellationToken cancellationToken = default);
    
    Task<List<CostTrackingSummaryResponse>> GetTopSpendersAsync(int topN, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
```

---

## Controllers

### 1. TemplatesController (Full Implementation)

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.API.Middleware;

namespace Listo.Notification.API.Controllers;

/// <summary>
/// Notification template management endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ManageTemplates")]
public class TemplatesController : ControllerBase
{
    private readonly ILogger<TemplatesController> _logger;
    private readonly ITemplateService _templateService;

    public TemplatesController(
        ILogger<TemplatesController> logger,
        ITemplateService templateService)
    {
        _logger = logger;
        _templateService = templateService;
    }

    /// <summary>
    /// Create a new notification template.
    /// </summary>
    /// <param name="request">Template creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created template details</returns>
    [HttpPost]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TemplateResponse>> CreateTemplate(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        _logger.LogInformation(
            "Creating template: TenantId={TenantId}, Key={TemplateKey}",
            tenantId, request.TemplateKey);

        var template = await _templateService.CreateAsync(tenantId, request, cancellationToken);

        return CreatedAtAction(
            nameof(GetTemplate),
            new { id = template.Id },
            template);
    }

    /// <summary>
    /// Get a template by ID.
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Template details</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateResponse>> GetTemplate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        var template = await _templateService.GetByIdAsync(tenantId, id, cancellationToken);

        return template != null ? Ok(template) : NotFound();
    }

    /// <summary>
    /// Get all templates with pagination.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of templates</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedTemplatesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedTemplatesResponse>> GetTemplates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        pageSize = Math.Min(pageSize, 100); // Max 100 per page

        var templates = await _templateService.GetAllAsync(tenantId, page, pageSize, cancellationToken);

        return Ok(templates);
    }

    /// <summary>
    /// Update an existing template.
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="request">Update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated template details</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TemplateResponse>> UpdateTemplate(
        Guid id,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        var template = await _templateService.UpdateAsync(tenantId, id, request, cancellationToken);

        return template != null ? Ok(template) : NotFound();
    }

    /// <summary>
    /// Delete a template.
    /// </summary>
    /// <param name="id">Template ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTemplate(
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        var deleted = await _templateService.DeleteAsync(tenantId, id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Render a template with provided variables (preview).
    /// </summary>
    /// <param name="request">Render request with template key and variables</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered content</returns>
    [HttpPost("render")]
    [ProducesResponseType(typeof(RenderTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RenderTemplateResponse>> RenderTemplate(
        [FromBody] RenderTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantContext.GetRequiredTenantId(HttpContext);

        var rendered = await _templateService.RenderAsync(tenantId, request, cancellationToken);

        return Ok(rendered);
    }
}
```

### 2. Additional Controllers Summary

Due to the comprehensive scope, here are the controller signatures. Full implementations follow the same patterns as `TemplatesController`:

#### PreferencesController
- `GET /api/v1/preferences` - Get user preferences
- `PUT /api/v1/preferences` - Update user preferences
- `PATCH /api/v1/preferences` - Partially update preferences

#### AdminController
- `GET /api/v1/admin/cost-tracking/summary` - Get cost summary
- `GET /api/v1/admin/cost-tracking/top-spenders` - Get top spenders
- `GET /api/v1/admin/rate-limits` - Get rate limit configurations
- `PUT /api/v1/admin/rate-limits/{key}` - Update rate limit
- `POST /api/v1/admin/budgets` - Create/update budget configuration

#### InternalController
- `POST /api/v1/internal/notifications/queue` - Queue notification (service-to-service)
- `GET /api/v1/internal/health` - Health check endpoint

#### WebhooksController
- `POST /api/v1/webhooks/twilio/status` - Twilio status callback
- `POST /api/v1/webhooks/sendgrid/events` - SendGrid event webhook
- `POST /api/v1/webhooks/fcm/delivery-status` - FCM delivery status

#### AttachmentsController
- `POST /api/v1/attachments` - Upload attachment
- `GET /api/v1/attachments/{id}` - Download attachment
- `GET /api/v1/attachments/{id}/metadata` - Get attachment metadata
- `DELETE /api/v1/attachments/{id}` - Delete attachment

#### Enhanced NotificationsController
- `POST /api/v1/notifications/batch` - Send notifications in batch
- `POST /api/v1/notifications/batch/schedule` - Schedule notifications in batch
- `GET /api/v1/notifications/batch/{batchId}/status` - Get batch status

---

## Azure Blob Storage Configuration

### 1. Configuration in `appsettings.json`

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "@Microsoft.KeyVault(SecretUri=https://your-keyvault.vault.azure.net/secrets/BlobStorageConnectionString/)",
    "ContainerName": "notification-attachments",
    "MaxFileSizeBytes": 10485760,
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".pptx"]
  },
  "FileUpload": {
    "MaxFileSizeBytes": 10485760,
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt"]
  }
}
```

### 2. Attachment Service Implementation

```csharp
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Listo.Notification.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AttachmentService> _logger;
    private readonly string _containerName;

    public AttachmentService(
        IConfiguration configuration,
        ILogger<AttachmentService> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureBlobStorage:ConnectionString"] 
            ?? throw new InvalidOperationException("Blob storage connection string not configured");
        
        _containerName = configuration["AzureBlobStorage:ContainerName"] ?? "notification-attachments";

        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
    }

    public async Task<AttachmentResponse> UploadAsync(
        Guid tenantId, 
        Guid userId, 
        UploadAttachmentRequest request, 
        CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var attachmentId = Guid.NewGuid();
        var blobName = $"{tenantId}/{attachmentId}/{request.File.FileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        var blobHttpHeaders = new BlobHttpHeaders
        {
            ContentType = request.File.ContentType
        };

        using var stream = request.File.OpenReadStream();
        await blobClient.UploadAsync(stream, blobHttpHeaders, cancellationToken: cancellationToken);

        var blobUrl = blobClient.Uri.ToString();

        _logger.LogInformation(
            "Uploaded attachment: Id={AttachmentId}, TenantId={TenantId}, FileName={FileName}, Size={Size}",
            attachmentId, tenantId, request.File.FileName, request.File.Length);

        return new AttachmentResponse
        {
            Id = attachmentId,
            FileName = request.File.FileName,
            ContentType = request.File.ContentType,
            SizeBytes = request.File.Length,
            BlobUrl = blobUrl,
            Description = request.Description,
            Type = request.Type,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = userId
        };
    }

    public async Task<Stream?> DownloadAsync(
        Guid tenantId, 
        Guid attachmentId, 
        CancellationToken cancellationToken = default)
    {
        // Query blob by prefix (tenantId/attachmentId)
        var prefix = $"{tenantId}/{attachmentId}/";
        
        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            var response = await blobClient.DownloadAsync(cancellationToken);
            return response.Value.Content;
        }

        return null;
    }

    public async Task<bool> DeleteAsync(
        Guid tenantId, 
        Guid attachmentId, 
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/{attachmentId}/";
        
        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            
            _logger.LogInformation(
                "Deleted attachment: Id={AttachmentId}, TenantId={TenantId}",
                attachmentId, tenantId);
            
            return true;
        }

        return false;
    }

    public async Task<AttachmentResponse?> GetMetadataAsync(
        Guid tenantId, 
        Guid attachmentId, 
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{tenantId}/{attachmentId}/";
        
        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            return new AttachmentResponse
            {
                Id = attachmentId,
                FileName = Path.GetFileName(blobItem.Name),
                ContentType = properties.Value.ContentType,
                SizeBytes = properties.Value.ContentLength,
                BlobUrl = blobClient.Uri.ToString(),
                Description = null,
                Type = AttachmentType.General,
                UploadedAt = properties.Value.CreatedOn.UtcDateTime,
                UploadedBy = Guid.Empty
            };
        }

        return null;
    }
}
```

---

## Registration in Program.cs

Add the following to `Program.cs`:

```csharp
// Register FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<SendNotificationRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Register Template service
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();

// Register Attachment service
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

// Register Admin services
builder.Services.AddScoped<ICostTrackingService, CostTrackingService>();
builder.Services.AddScoped<IRateLimitManagementService, RateLimitManagementService>();

// Enable XML documentation for Swagger
builder.Services.AddSwaggerGen(c =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});
```

In `Listo.Notification.API.csproj`, add:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

---

## API Endpoints Documentation

See comprehensive `notification_api_endpoints.md` (next file to be created).

---

## Implementation Checklist

- [x] FluentValidation.AspNetCore package installed
- [x] Azure.Storage.Blobs package installed
- [x] Template DTOs created
- [ ] All DTOs created (Batch, Attachment, Admin, Internal, Webhooks)
- [ ] All FluentValidation validators created
- [ ] Template service and repository implemented
- [ ] Attachment service implemented with Azure Blob Storage
- [ ] Admin services implemented
- [ ] All controllers created with XML documentation
- [ ] NotificationsController enhanced with batch operations
- [ ] Program.cs updated with service registrations
- [ ] API documentation created (notification_api_endpoints.md)
- [ ] Swagger configuration enhanced
- [ ] Build verification successful

---

## Next Steps

1. Create remaining DTO files (copy patterns from this guide)
2. Create remaining validator files
3. Implement remaining services
4. Create remaining controllers
5. Update Program.cs with registrations
6. Create API documentation
7. Verify build
8. Update TODO.md

This guide provides the blueprint for complete Section 10-13 implementation. Follow the patterns established in `TemplatesController` and the validators shown above for consistent implementation across all endpoints.
