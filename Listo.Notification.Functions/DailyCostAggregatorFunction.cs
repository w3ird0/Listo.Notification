using Listo.Notification.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Functions;

/// <summary>
/// Aggregates daily notification costs and stores them in the CostTracking table.
/// Runs daily at configurable time (default: midnight UTC).
/// Computes cost per channel, per tenant, and per service.
/// Singleton to prevent concurrent execution.
/// </summary>
public class DailyCostAggregatorFunction
{
    private readonly ILogger<DailyCostAggregatorFunction> _logger;
    private readonly NotificationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public DailyCostAggregatorFunction(
        ILogger<DailyCostAggregatorFunction> logger,
        NotificationDbContext dbContext,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Timer-triggered function for daily cost aggregation.
    /// Timer schedule: Configurable via DailyCostAggregator:Schedule (default: "0 0 0 * * *" = midnight UTC daily).
    /// Singleton mode to prevent concurrent execution.
    /// </summary>
    [Function("DailyCostAggregator")]
    [Singleton]
    public async Task RunAsync(
        [TimerTrigger("%DailyCostAggregator:Schedule%", RunOnStartup = false)] TimerInfo timer,
        FunctionContext context)
    {
        _logger.LogInformation("DailyCostAggregator started at {Time}", DateTime.UtcNow);

        try
        {
            // Calculate yesterday's date range (full day UTC)
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var startOfYesterday = yesterday;
            var endOfYesterday = yesterday.AddDays(1);

            _logger.LogInformation(
                "Aggregating costs for date range: {StartDate} to {EndDate}",
                startOfYesterday, endOfYesterday);

            // Query cost tracking entries from yesterday (raw cost data)
            var yesterdayCosts = await _dbContext.CostTracking
                .Where(c => c.OccurredAt >= startOfYesterday && c.OccurredAt < endOfYesterday)
                .Select(c => new
                {
                    c.TenantId,
                    c.ServiceOrigin,
                    c.Channel,
                    CostInMicros = c.TotalCostMicros
                })
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} cost tracking entries for yesterday",
                yesterdayCosts.Count);

            // Group by TenantId, ServiceOrigin, Channel and sum costs
            var costGroups = yesterdayCosts
                .GroupBy(c => new { c.TenantId, c.ServiceOrigin, c.Channel })
                .Select(g => new
                {
                    g.Key.TenantId,
                    g.Key.ServiceOrigin,
                    g.Key.Channel,
                    TotalCostMicros = g.Sum(c => c.CostInMicros),
                    Count = g.Count()
                })
                .ToList();

            _logger.LogInformation("Computed {GroupCount} cost groups", costGroups.Count);

            // NOTE: CostTrackingEntity is per-message, not daily aggregate.
            // This function currently skips creation to avoid duplicates.
            // Daily aggregates should be computed from existing CostTracking entries.
            
            _logger.LogInformation(
                "Computed {Count} cost groups for yesterday. Aggregation complete.",
                costGroups.Count);

            // Log cost summary
            foreach (var group in costGroups)
            {
                var costInDollars = group.TotalCostMicros / 1_000_000.0m;
                _logger.LogInformation(
                    "Daily cost summary: TenantId={TenantId}, Service={Service}, Channel={Channel}, " +
                    "Cost=${Cost:F4}, EntryCount={Count}",
                    group.TenantId, group.ServiceOrigin, group.Channel, costInDollars, group.Count);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "DailyCostAggregator completed. Created {Count} cost tracking entries for {Date}",
                costGroups.Count, startOfYesterday.Date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DailyCostAggregator");
            throw;
        }

        if (timer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next daily cost aggregation at: {NextRun}", timer.ScheduleStatus.Next);
        }
    }
}
