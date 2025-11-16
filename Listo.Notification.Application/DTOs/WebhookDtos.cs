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

/// <summary>
/// Generic webhook response.
/// </summary>
public record WebhookResponse
{
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public required DateTime ProcessedAt { get; init; }
}
