namespace Listo.Notification.Application.Interfaces;

/// <summary>
/// Service for enforcing rate limits using Redis token bucket algorithm.
/// </summary>
public interface IRateLimiterService
{
    /// <summary>
    /// Checks if a request is allowed under rate limiting rules.
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="serviceOrigin">Service origin (auth, orders, etc.)</param>
    /// <param name="channel">Notification channel</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if allowed, false if rate limit exceeded</returns>
    Task<bool> IsAllowedAsync(
        Guid tenantId,
        string userId,
        string serviceOrigin,
        string channel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the remaining capacity for a user.
    /// </summary>
    Task<int> GetRemainingCapacityAsync(
        Guid tenantId,
        string userId,
        string channel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the time until reset for a user's rate limit.
    /// </summary>
    Task<TimeSpan?> GetTimeUntilResetAsync(
        Guid tenantId,
        string userId,
        string channel,
        CancellationToken cancellationToken = default);
}
