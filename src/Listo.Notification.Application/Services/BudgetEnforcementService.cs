using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Application.Services;

/// <summary>
/// Enforces budget limits per-tenant and per-service.
/// Blocks non-critical notifications when budget reaches 100%, allows high-priority notifications.
/// </summary>
public class BudgetEnforcementService
{
    private readonly ILogger<BudgetEnforcementService> _logger;

    // Cost per channel in micros (1 micro = 1/1,000,000 of currency unit)
    private static readonly Dictionary<string, long> ChannelCostMicros = new()
    {
        { "email", 950 },      // $0.00095 per email (SendGrid)
        { "sms", 7900 },       // $0.0079 per SMS (Twilio US)
        { "push", 0 },         // Free (FCM)
        { "in-app", 0 }        // Free (SignalR)
    };

    public BudgetEnforcementService(
        ILogger<BudgetEnforcementService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if a notification can be sent based on budget constraints.
    /// NOTE: This is a placeholder - actual implementation should be in Infrastructure layer
    /// with proper DbContext access.
    /// </summary>
    public Task<BudgetCheckResult> CheckBudgetAsync(
        Guid tenantId,
        string serviceOrigin,
        string channel,
        Priority priority,
        int segmentCount = 1,
        CancellationToken cancellationToken = default)
    {
        // Placeholder implementation - budget check logic should be implemented
        // in Infrastructure layer with proper DbContext access
        _logger.LogInformation(
            "Budget check requested for tenant {TenantId}, service {Service}, channel {Channel}, priority {Priority}",
            tenantId, serviceOrigin, channel, priority);

        // For now, allow all notifications (budget enforcement to be implemented in Infrastructure)
        var result = BudgetCheckResult.CreateAllowed();

        return Task.FromResult(result);
    }

    /// <summary>
    /// Records the actual cost of a sent notification for budget tracking.
    /// NOTE: Implementation should be in Infrastructure layer.
    /// </summary>
    public Task RecordCostAsync(
        Guid tenantId,
        string serviceOrigin,
        string channel,
        string currency,
        int segmentCount = 1,
        CancellationToken cancellationToken = default)
    {
        var costMicros = CalculateCostMicros(channel, segmentCount);

        _logger.LogInformation(
            "Cost recording requested for tenant {TenantId}, service {ServiceOrigin}, channel {Channel}: {Cost} micros",
            tenantId, serviceOrigin, channel, costMicros);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current budget utilization percentage for a tenant/service/channel.
    /// NOTE: Implementation should be in Infrastructure layer.
    /// </summary>
    public Task<double> GetBudgetUtilizationAsync(
        Guid tenantId,
        string serviceOrigin,
        string channel,
        CancellationToken cancellationToken = default)
    {
        // Placeholder - should query database in Infrastructure layer
        return Task.FromResult(0.0);
    }

    private static long CalculateCostMicros(string channel, int segmentCount)
    {
        var channelLower = channel.ToLowerInvariant();

        if (!ChannelCostMicros.TryGetValue(channelLower, out var costPerSegment))
        {
            // Unknown channel - assume free
            return 0;
        }

        // For SMS, multiply by segment count (multi-part messages)
        if (channelLower == "sms")
        {
            return costPerSegment * segmentCount;
        }

        return costPerSegment;
    }
}

/// <summary>
/// Result of a budget check.
/// </summary>
public record BudgetCheckResult
{
    public required bool Allowed { get; init; }
    public string? Reason { get; init; }
    public double Utilization { get; init; }
    public long RemainingMicros { get; init; }
    public long? LimitMicros { get; init; }
    public long? CurrentSpendMicros { get; init; }
    public string? WarningMessage { get; init; }

    public static BudgetCheckResult CreateAllowed(
        double utilization = 0.0,
        long remaining = long.MaxValue,
        string? warningMessage = null) =>
        new()
        {
            Allowed = true,
            Utilization = utilization,
            RemainingMicros = remaining,
            WarningMessage = warningMessage
        };

    public static BudgetCheckResult Denied(
        string reason,
        double utilization,
        long limit,
        long currentSpend) =>
        new()
        {
            Allowed = false,
            Reason = reason,
            Utilization = utilization,
            RemainingMicros = 0,
            LimitMicros = limit,
            CurrentSpendMicros = currentSpend
        };
}
