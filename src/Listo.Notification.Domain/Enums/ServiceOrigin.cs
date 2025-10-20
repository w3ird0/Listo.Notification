namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the Listo service that originated the notification request.
/// Used for cost attribution, rate limiting, and analytics.
/// </summary>
public enum ServiceOrigin
{
    /// <summary>
    /// Listo.Auth service - authentication, verification, password reset
    /// </summary>
    Auth = 1,

    /// <summary>
    /// Listo.Orders service - food delivery orders, driver assignments
    /// </summary>
    Orders = 2,

    /// <summary>
    /// Listo.RideSharing service - ride bookings, driver notifications
    /// </summary>
    RideSharing = 3,

    /// <summary>
    /// Listo.Products service - product catalog notifications
    /// </summary>
    Products = 4,

    /// <summary>
    /// System-level or admin notifications
    /// </summary>
    System = 5
}
