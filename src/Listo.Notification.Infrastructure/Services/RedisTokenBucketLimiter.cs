using Listo.Notification.Application.DTOs;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Listo.Notification.Infrastructure.Services;

/// <summary>
/// Redis-based token bucket rate limiter with atomic Lua script execution.
/// Enforces per-user and per-service rate limits with burst capacity.
/// </summary>
public class RedisTokenBucketLimiter : IRateLimiterService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<RedisTokenBucketLimiter> _logger;

    // Lua script for atomic token bucket operations
    private const string LuaScript = @"
        local key = KEYS[1]
        local maxTokens = tonumber(ARGV[1])
        local refillRate = tonumber(ARGV[2])
        local now = tonumber(ARGV[3])
        local windowSeconds = tonumber(ARGV[4])
        local burstSize = tonumber(ARGV[5])

        -- Get current token count and last refill time
        local tokens = tonumber(redis.call('HGET', key, 'tokens')) or maxTokens
        local lastRefill = tonumber(redis.call('HGET', key, 'last_refill')) or now

        -- Calculate tokens to add based on time elapsed
        local elapsed = now - lastRefill
        local tokensToAdd = math.floor(elapsed * refillRate)
        
        -- Refill tokens up to max + burst
        tokens = math.min(tokens + tokensToAdd, maxTokens + burstSize)

        -- Check if request can be allowed
        if tokens >= 1 then
            tokens = tokens - 1
            redis.call('HSET', key, 'tokens', tokens)
            redis.call('HSET', key, 'last_refill', now)
            redis.call('EXPIRE', key, windowSeconds)
            return {1, tokens}  -- Allowed, remaining capacity
        else
            return {0, 0}  -- Denied, no capacity
        end
    ";

    public RedisTokenBucketLimiter(
        IConnectionMultiplexer redis,
        NotificationDbContext dbContext,
        ILogger<RedisTokenBucketLimiter> logger)
    {
        _redis = redis;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> IsAllowedAsync(
        Guid tenantId,
        string userId,
        string serviceOrigin,
        string channel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get rate limiting configuration
            var config = await GetRateLimitConfigAsync(tenantId, serviceOrigin, channel, cancellationToken);
            if (config == null || !config.Enabled)
                return true; // No limits configured or disabled

            // Check per-user rate limit
            var userAllowed = await CheckUserRateLimitAsync(tenantId, userId, channel, config);
            if (!userAllowed)
            {
                _logger.LogWarning(
                    "User {UserId} in tenant {TenantId} exceeded rate limit for channel {Channel}",
                    userId, tenantId, channel);
                return false;
            }

            // Check per-service rate limit
            var serviceAllowed = await CheckServiceRateLimitAsync(tenantId, serviceOrigin, channel, config);
            if (!serviceAllowed)
            {
                _logger.LogWarning(
                    "Service {ServiceOrigin} in tenant {TenantId} exceeded rate limit for channel {Channel}",
                    serviceOrigin, tenantId, channel);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for user {UserId}", userId);
            // Fail open - allow request if rate limiting check fails
            return true;
        }
    }

    public async Task<int> GetRemainingCapacityAsync(
        Guid tenantId,
        string userId,
        string channel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await GetRateLimitConfigAsync(tenantId, "*", channel, cancellationToken);
            if (config == null)
                return int.MaxValue;

            var key = config.GetUserRateLimitKey(tenantId, userId);
            var db = _redis.GetDatabase();

            var tokens = await db.HashGetAsync(key, "tokens");
            return tokens.HasValue ? (int)tokens : config.PerUserMax;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remaining capacity for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<TimeSpan?> GetTimeUntilResetAsync(
        Guid tenantId,
        string userId,
        string channel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await GetRateLimitConfigAsync(tenantId, "*", channel, cancellationToken);
            if (config == null)
                return null;

            var key = config.GetUserRateLimitKey(tenantId, userId);
            var db = _redis.GetDatabase();

            var ttl = await db.KeyTimeToLiveAsync(key);
            return ttl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting time until reset for user {UserId}", userId);
            return null;
        }
    }

    private async Task<bool> CheckUserRateLimitAsync(
        Guid tenantId,
        string userId,
        string channel,
        RateLimitingEntity config)
    {
        var key = config.GetUserRateLimitKey(tenantId, userId);
        var db = _redis.GetDatabase();

        var refillRate = (double)config.PerUserMax / config.PerUserWindowSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await db.ScriptEvaluateAsync(
            LuaScript,
            new RedisKey[] { key },
            new RedisValue[]
            {
                config.PerUserMaxCap,
                refillRate,
                now,
                config.PerUserWindowSeconds,
                config.BurstSize
            });

        var values = (RedisValue[])result;
        var allowed = (int)values[0];

        return allowed == 1;
    }

    private async Task<bool> CheckServiceRateLimitAsync(
        Guid tenantId,
        string serviceOrigin,
        string channel,
        RateLimitingEntity config)
    {
        var key = config.GetServiceRateLimitKey(tenantId);
        var db = _redis.GetDatabase();

        var refillRate = (double)config.PerServiceMax / config.PerServiceWindowSeconds;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await db.ScriptEvaluateAsync(
            LuaScript,
            new RedisKey[] { key },
            new RedisValue[]
            {
                config.PerServiceMaxCap,
                refillRate,
                now,
                config.PerServiceWindowSeconds,
                config.BurstSize
            });

        var values = (RedisValue[])result;
        var allowed = (int)values[0];

        return allowed == 1;
    }

    // Admin Operations
    public async Task<object> GetRateLimitConfigAsync(
        string tenantId,
        string? userId,
        string? channel,
        CancellationToken cancellationToken = default)
    {
        var tenantGuid = Guid.TryParse(tenantId, out var guid) ? guid : (Guid?)null;
        
        var query = _dbContext.RateLimiting.AsQueryable();
        
        if (tenantGuid.HasValue)
            query = query.Where(r => r.TenantId == tenantGuid.Value);
            
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(r => r.Channel == channel);
        
        var configs = await query.ToListAsync(cancellationToken);
        
        return new 
        {
            tenantId,
            channel,
            configs = configs.Select(c => new
            {
                c.ConfigId,
                c.TenantId,
                c.ServiceOrigin,
                c.Channel,
                c.PerUserMax,
                c.PerUserWindowSeconds,
                c.PerServiceMax,
                c.PerServiceWindowSeconds,
                c.Enabled
            })
        };
    }

    public async Task UpdateRateLimitConfigAsync(
        UpdateRateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantGuid = Guid.TryParse(request.TenantId, out var guid) ? guid : (Guid?)null;
        
        // Find existing config or create new one
        var config = await _dbContext.RateLimiting
            .Where(r => r.TenantId == tenantGuid && r.Channel == request.Channel)
            .FirstOrDefaultAsync(cancellationToken);
        
        if (config == null)
        {
            config = new RateLimitingEntity
            {
                ConfigId = Guid.NewGuid(),
                TenantId = tenantGuid,
                Channel = request.Channel ?? "*",
                ServiceOrigin = "*",
                PerUserMax = request.Limit,
                PerUserWindowSeconds = request.WindowSeconds,
                PerUserMaxCap = request.BurstCapacity ?? request.Limit * 2,
                PerServiceMax = request.Limit * 10,
                PerServiceWindowSeconds = request.WindowSeconds,
                PerServiceMaxCap = request.Limit * 20,
                BurstSize = request.BurstCapacity ?? request.Limit,
                Enabled = true
            };
            
            _dbContext.RateLimiting.Add(config);
        }
        else
        {
            config.PerUserMax = request.Limit;
            config.PerUserWindowSeconds = request.WindowSeconds;
            config.PerUserMaxCap = request.BurstCapacity ?? request.Limit * 2;
            config.BurstSize = request.BurstCapacity ?? request.Limit;
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Updated rate limit config for tenant {TenantId}, channel {Channel}",
            request.TenantId, request.Channel);
    }

    private async Task<RateLimitingEntity?> GetRateLimitConfigAsync(
        Guid tenantId,
        string serviceOrigin,
        string channel,
        CancellationToken cancellationToken)
    {
        // Try to get tenant-specific config first
        var config = await _dbContext.RateLimiting
            .Where(r => r.TenantId == tenantId && r.ServiceOrigin == serviceOrigin && r.Channel == channel)
            .FirstOrDefaultAsync(cancellationToken);

        if (config != null)
            return config;

        // Fall back to wildcard service origin with tenant
        config = await _dbContext.RateLimiting
            .Where(r => r.TenantId == tenantId && r.ServiceOrigin == "*" && r.Channel == channel)
            .FirstOrDefaultAsync(cancellationToken);

        if (config != null)
            return config;

        // Fall back to global config with specific service
        config = await _dbContext.RateLimiting
            .Where(r => r.TenantId == null && r.ServiceOrigin == serviceOrigin && r.Channel == channel)
            .FirstOrDefaultAsync(cancellationToken);

        if (config != null)
            return config;

        // Fall back to global wildcard config
        return await _dbContext.RateLimiting
            .Where(r => r.TenantId == null && r.ServiceOrigin == "*" && r.Channel == channel)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
