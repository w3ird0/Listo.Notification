namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents external providers used for notification delivery.
/// </summary>
public enum NotificationProvider
{
    /// <summary>
    /// Firebase Cloud Messaging for push notifications
    /// </summary>
    FCM = 1,

    /// <summary>
    /// Twilio for SMS notifications
    /// </summary>
    Twilio = 2,

    /// <summary>
    /// SendGrid for email notifications
    /// </summary>
    SendGrid = 3,

    /// <summary>
    /// Azure Communication Services for email notifications
    /// </summary>
    ACS = 4,

    /// <summary>
    /// AWS SNS for SMS notifications (secondary provider)
    /// </summary>
    AwsSns = 5,

    /// <summary>
    /// SignalR for in-app real-time messaging
    /// </summary>
    SignalR = 6
}
