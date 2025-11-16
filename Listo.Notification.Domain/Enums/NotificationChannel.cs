namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the delivery channel for a notification.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// Push notification via FCM (Firebase Cloud Messaging)
    /// </summary>
    Push = 1,

    /// <summary>
    /// SMS notification via Twilio or AWS SNS
    /// </summary>
    Sms = 2,

    /// <summary>
    /// Email notification via SendGrid or Azure Communication Services
    /// </summary>
    Email = 3,

    /// <summary>
    /// In-app notification via SignalR
    /// </summary>
    InApp = 4
}
