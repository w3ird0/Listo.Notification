namespace Listo.Notification.Domain.Enums;

/// <summary>
/// Represents the type of actor that performed an audited action.
/// Used in audit logging for compliance tracking.
/// </summary>
public enum ActorType
{
    /// <summary>
    /// Action performed by an end user (customer, driver)
    /// </summary>
    User = 1,

    /// <summary>
    /// Action performed by another Listo service
    /// </summary>
    Service = 2,

    /// <summary>
    /// Action performed by the notification system itself
    /// </summary>
    System = 3,

    /// <summary>
    /// Action performed by an administrator
    /// </summary>
    Admin = 4
}
