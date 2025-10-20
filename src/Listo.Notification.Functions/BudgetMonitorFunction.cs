using Listo.Notification.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Functions;

/// <summary>
/// Monitors budget utilization hourly and sends alerts at 80% and 100% thresholds.
/// Computes current month spend from CostTracking table.
/// Sends alerts via Service Bus and email (placeholders).
/// Singleton to prevent concurrent execution.
/// </summary>
public class BudgetMonitorFunction
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<BudgetMonitorFunction> _logger;
    private readonly IConfiguration _configuration;

    public BudgetMonitorFunction(
        NotificationDbContext dbContext,
        ILogger<BudgetMonitorFunction> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Runs every hour to check budget thresholds.
    /// Timer schedule: Configurable via BudgetMonitor:Schedule (default: "0 0 * * * *" = every hour).
    /// Singleton mode to prevent concurrent execution.
    /// NOTE: Full Service Bus integration requires Azure.Messaging.ServiceBus and BudgetConfigEntity.
    /// </summary>
    [Function("BudgetMonitor")]
    [Singleton]
    public async Task RunAsync(
        [TimerTrigger("%BudgetMonitor:Schedule%", RunOnStartup = false)] TimerInfo timer,
        FunctionContext context)
    {
        _logger.LogInformation("BudgetMonitor started at {Time}", DateTime.UtcNow);

        try
        {            
            // Calculate current month date range
            var now = DateTime.UtcNow;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            _logger.LogInformation(
                "Checking budget for month: {StartDate} to {EndDate}",
                firstDayOfMonth, lastDayOfMonth);

            // TODO: Query BudgetConfigEntity when available
            // For now, use hardcoded thresholds or configuration
            var budgetThreshold80 = _configuration.GetValue<decimal>("Budget:Threshold80Percent", 0.8m);
            var budgetThreshold100 = _configuration.GetValue<decimal>("Budget:Threshold100Percent", 1.0m);

            // Query current month spend from CostTracking table grouped by tenant/service
            var currentMonthSpend = await _dbContext.CostTracking
                .Where(c => c.OccurredAt >= firstDayOfMonth && c.OccurredAt <= lastDayOfMonth)
                .GroupBy(c => new { c.TenantId, c.ServiceOrigin })
                .Select(g => new
                {
                    g.Key.TenantId,
                    g.Key.ServiceOrigin,
                    TotalSpendMicros = g.Sum(c => c.TotalCostMicros),
                    ChannelBreakdown = g.GroupBy(c => c.Channel)
                        .Select(ch => new
                        {
                            Channel = ch.Key,
                            CostMicros = ch.Sum(c => c.TotalCostMicros)
                        })
                        .ToList()
                })
                .ToListAsync();

            _logger.LogInformation(
                "Found {Count} tenant/service combinations with spend this month",
                currentMonthSpend.Count);

            foreach (var spend in currentMonthSpend)
            {
                var spendInDollars = spend.TotalSpendMicros / 1_000_000.0m;
                _logger.LogInformation(
                    "Current spend: TenantId={TenantId}, Service={Service}, Amount=${Amount:F4}",
                    spend.TenantId, spend.ServiceOrigin, spendInDollars);

                // TODO: Check against BudgetConfigEntity thresholds
                // TODO: Track alert flags (Alert80PercentSent, Alert100PercentSent) in BudgetConfigEntity
                // TODO: Send alerts via Service Bus when thresholds exceeded
                // Example:
                // if (spend.TotalSpend >= budget * budgetThreshold80 && !config.Alert80PercentSent)
                // {
                //     await SendBudgetAlertAsync(spend, "80% budget threshold reached");
                //     config.Alert80PercentSent = true;
                // }
                // if (spend.TotalSpend >= budget * budgetThreshold100 && !config.Alert100PercentSent)
                // {
                //     await SendBudgetAlertAsync(spend, "100% budget threshold reached");
                //     config.Alert100PercentSent = true;
                // }
            }

            // await _dbContext.SaveChangesAsync(); // Uncomment when BudgetConfigEntity tracking is implemented

            _logger.LogInformation(
                "BudgetMonitor completed. Checked {Count} tenant/service budgets",
                currentMonthSpend.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in BudgetMonitor");
            throw;
        }

        if (timer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next budget check at: {NextRun}", timer.ScheduleStatus.Next);
        }
    }

    // TODO: Implement SendBudgetAlertAsync when Service Bus is integrated
    // private async Task SendBudgetAlertAsync(object spend, string message)
    // {
    //     // Send to Service Bus topic or queue
    //     // Send email alert to tenant admins
    // }
}
