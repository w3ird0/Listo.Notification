using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Base interface for notification providers across all channels.
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// Gets the channel this provider handles.
    /// </summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// Gets the provider name (e.g., "Twilio", "SendGrid", "FCM").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates if this provider is a fallback/secondary provider.
    /// </summary>
    bool IsFallback { get; }

    /// <summary>
    /// Sends a notification through this provider.
    /// </summary>
    /// <param name="request">Notification delivery request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-specific delivery result.</returns>
    Task<DeliveryResult> SendAsync(DeliveryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the provider is currently healthy and available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if provider is healthy.</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// SMS provider interface.
/// </summary>
public interface ISmsProvider : INotificationProvider
{
}

/// <summary>
/// Email provider interface.
/// </summary>
public interface IEmailProvider : INotificationProvider
{
}

/// <summary>
/// Push notification provider interface.
/// </summary>
public interface IPushProvider : INotificationProvider
{
}

/// <summary>
/// Notification delivery request containing all necessary data.
/// </summary>
public record DeliveryRequest(
    Guid NotificationId,
    Guid TenantId,
    NotificationChannel Channel,
    string Recipient,
    string Subject,
    string Body,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// Result of a notification delivery attempt.
/// </summary>
public record DeliveryResult(
    bool Success,
    string? ProviderId,
    string? ProviderMessageId,
    string? ErrorMessage = null,
    Dictionary<string, string>? ProviderMetadata = null);
