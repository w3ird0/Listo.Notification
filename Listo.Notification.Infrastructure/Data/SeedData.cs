using Listo.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Listo.Notification.Infrastructure.Data;

/// <summary>
/// Provides seed data for default retry policies and rate limiting configurations.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Seeds default data into the database.
    /// Should be called during application startup or migration.
    /// </summary>
    public static async Task SeedDefaultsAsync(NotificationDbContext context)
    {
        await SeedRetryPoliciesAsync(context);
        await SeedRateLimitingAsync(context);
    }

    private static async Task SeedRetryPoliciesAsync(NotificationDbContext context)
    {
        if (await context.RetryPolicies.AnyAsync())
            return; // Already seeded

        var policies = new List<RetryPolicyEntity>
        {
            // Default policy for all services and channels
            new RetryPolicyEntity
            {
                PolicyId = Guid.NewGuid(),
                ServiceOrigin = "*",
                Channel = "*",
                MaxAttempts = 6,
                BaseDelaySeconds = 5,
                BackoffFactor = 2.0m,
                JitterMs = 1000,
                TimeoutSeconds = 30,
                Enabled = true
            },
            // Faster retry for driver assignment (critical path)
            new RetryPolicyEntity
            {
                PolicyId = Guid.NewGuid(),
                ServiceOrigin = "orders",
                Channel = "push",
                MaxAttempts = 3,
                BaseDelaySeconds = 2,
                BackoffFactor = 2.0m,
                JitterMs = 500,
                TimeoutSeconds = 15,
                Enabled = true
            },
            // OTP/2FA SMS retry policy
            new RetryPolicyEntity
            {
                PolicyId = Guid.NewGuid(),
                ServiceOrigin = "auth",
                Channel = "sms",
                MaxAttempts = 4,
                BaseDelaySeconds = 3,
                BackoffFactor = 2.0m,
                JitterMs = 500,
                TimeoutSeconds = 20,
                Enabled = true
            }
        };

        await context.RetryPolicies.AddRangeAsync(policies);
        await context.SaveChangesAsync();
    }

    private static async Task SeedRateLimitingAsync(NotificationDbContext context)
    {
        if (await context.RateLimiting.AnyAsync())
            return; // Already seeded

        var limits = new List<RateLimitingEntity>
        {
            // Email rate limits
            new RateLimitingEntity
            {
                ConfigId = Guid.NewGuid(),
                TenantId = null, // Global default
                ServiceOrigin = "*",
                Channel = "email",
                PerUserWindowSeconds = 3600, // 1 hour
                PerUserMax = 60,
                PerUserMaxCap = 100,
                PerServiceWindowSeconds = 86400, // 1 day
                PerServiceMax = 50000,
                PerServiceMaxCap = 75000,
                BurstSize = 20,
                Enabled = true
            },
            // SMS rate limits
            new RateLimitingEntity
            {
                ConfigId = Guid.NewGuid(),
                TenantId = null, // Global default
                ServiceOrigin = "*",
                Channel = "sms",
                PerUserWindowSeconds = 3600,
                PerUserMax = 60,
                PerUserMaxCap = 100,
                PerServiceWindowSeconds = 86400,
                PerServiceMax = 10000,
                PerServiceMaxCap = 15000,
                BurstSize = 20,
                Enabled = true
            },
            // Push notification rate limits
            new RateLimitingEntity
            {
                ConfigId = Guid.NewGuid(),
                TenantId = null, // Global default
                ServiceOrigin = "*",
                Channel = "push",
                PerUserWindowSeconds = 3600,
                PerUserMax = 60,
                PerUserMaxCap = 100,
                PerServiceWindowSeconds = 86400,
                PerServiceMax = 200000,
                PerServiceMaxCap = 300000,
                BurstSize = 20,
                Enabled = true
            },
            // In-app messaging rate limits (effectively unlimited)
            new RateLimitingEntity
            {
                ConfigId = Guid.NewGuid(),
                TenantId = null, // Global default
                ServiceOrigin = "*",
                Channel = "inApp",
                PerUserWindowSeconds = 3600,
                PerUserMax = 1000,
                PerUserMaxCap = 2000,
                PerServiceWindowSeconds = 86400,
                PerServiceMax = 999999999,
                PerServiceMaxCap = 999999999,
                BurstSize = 100,
                Enabled = true
            }
        };

        await context.RateLimiting.AddRangeAsync(limits);
        await context.SaveChangesAsync();
    }
}
