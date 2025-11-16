using Listo.Notification.Application.DTOs;

namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service client for communicating with Listo.Auth service.
/// Provides device token lookup for push notification delivery.
/// </summary>
public interface IAuthServiceClient
{
    /// <summary>
    /// Get active device tokens for a specific user from Listo.Auth service.
    /// </summary>
    /// <param name="userId">User ID to fetch devices for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of device tokens ready for push notifications</returns>
    Task<List<DeviceTokenDto>> GetUserDeviceTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
