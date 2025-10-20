using Listo.Notification.Domain.Enums;

namespace Listo.Notification.Application.DTOs;

/// <summary>
/// Request to update rate limit configuration.
/// </summary>
public record UpdateRateLimitRequest
{
    public string? TenantId { get; init; }
    public string? Channel { get; init; }
    public required string Key { get; init; } // e.g., "user:{userId}", "service:{serviceOrigin}", "tenant:{tenantId}"
    public required int Limit { get; init; }
    public required int WindowSeconds { get; init; }
    public int? BurstCapacity { get; init; }
}

/// <summary>
/// Rate limit configuration response.
/// </summary>
public record RateLimitResponse
{
    public required string Key { get; init; }
    public required int Limit { get; init; }
    public required int WindowSeconds { get; init; }
    public int? BurstCapacity { get; init; }
    public required int CurrentUsage { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Cost tracking summary response.
/// </summary>
public record CostTrackingSummaryResponse
{
    public required Guid TenantId { get; init; }
    public ServiceOrigin? ServiceOrigin { get; init; }
    public required decimal TotalCostUsd { get; init; }
    public required int NotificationCount { get; init; }
    public required Dictionary<NotificationChannel, decimal> CostByChannel { get; init; }
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
}

/// <summary>
/// Budget configuration request.
/// </summary>
public record UpdateBudgetRequest
{
    public required Guid TenantId { get; init; }
    public ServiceOrigin? ServiceOrigin { get; init; }
    public required decimal MonthlyBudgetUsd { get; init; }
    public bool EnableAlerts { get; init; } = true;
    public decimal AlertThreshold80Percent { get; init; } = 0.8m;
    public decimal AlertThreshold100Percent { get; init; } = 1.0m;
}

/// <summary>
/// Budget configuration response.
/// </summary>
public record BudgetResponse
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public ServiceOrigin? ServiceOrigin { get; init; }
    public required decimal MonthlyBudgetUsd { get; init; }
    public required decimal CurrentSpendUsd { get; init; }
    public required decimal RemainingBudgetUsd { get; init; }
    public required decimal PercentageUsed { get; init; }
    public required bool EnableAlerts { get; init; }
    public bool Alert80PercentSent { get; init; }
    public bool Alert100PercentSent { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
