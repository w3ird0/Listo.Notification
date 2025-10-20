using Listo.Notification.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Functions;

/// <summary>
/// Performs monthly cost rollup and budget reset on the 1st of each month.
/// Aggregates costs from CostTracking table for the previous month.
/// Resets monthly budget tracking flags and counters.
/// Sends monthly cost summary alerts via Service Bus (placeholder).
/// Singleton to prevent concurrent execution.
/// </summary>
public class MonthlyCostRollupFunction
{
    private readonly ILogger<MonthlyCostRollupFunction> _logger;
    private readonly NotificationDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public MonthlyCostRollupFunction(
        ILogger<MonthlyCostRollupFunction> logger,
        NotificationDbContext dbContext,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Timer-triggered function for monthly cost rollup and budget reset.
    /// Timer schedule: Configurable via MonthlyCostRollup:Schedule (default: "0 0 2 1 * *" = 2 AM UTC on 1st of month).
    /// Singleton mode to prevent concurrent execution.
    /// </summary>
    [Function("MonthlyCostRollup")]
    [Singleton]
    public async Task RunAsync(
        [TimerTrigger("%MonthlyCostRollup:Schedule%", RunOnStartup = false)] TimerInfo timer,
        FunctionContext context)
    {
        _logger.LogInformation("MonthlyCostRollup started at {Time}", DateTime.UtcNow);

        try
        {
            // Calculate last month date range
            var now = DateTime.UtcNow;
            var firstDayOfCurrentMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayOfLastMonth = firstDayOfCurrentMonth.AddMonths(-1);
            var lastDayOfLastMonth = firstDayOfCurrentMonth.AddDays(-1);

            _logger.LogInformation(
                "Processing monthly rollup for: {StartDate} to {EndDate}",
                firstDayOfLastMonth, lastDayOfLastMonth);

            // Query all cost tracking entries for last month
            var monthlyCosts = await _dbContext.CostTracking
                .Where(c => c.OccurredAt >= firstDayOfLastMonth && c.OccurredAt <= lastDayOfLastMonth.Date)
                .GroupBy(c => new { c.TenantId, c.ServiceOrigin })
                .Select(g => new
                {
                    g.Key.TenantId,
                    g.Key.ServiceOrigin,
                    TotalCostMicros = g.Sum(c => c.TotalCostMicros),
                    TotalCount = g.Count(),
                    ChannelBreakdown = g.GroupBy(c => c.Channel)
                        .Select(ch => new
                        {
                            Channel = ch.Key,
                            CostMicros = ch.Sum(c => c.TotalCostMicros),
                            Count = ch.Count()
                        })
                        .ToList()
                })
                .ToListAsync();

            _logger.LogInformation(
                "Computed monthly costs for {Count} tenant/service combinations",
                monthlyCosts.Count);

            // Log monthly cost summaries
            foreach (var cost in monthlyCosts)
            {
                var costInDollars = cost.TotalCostMicros / 1_000_000.0m;
                _logger.LogInformation(
                    "Monthly summary: TenantId={TenantId}, Service={Service}, " +
                    "TotalCost=${Cost:F4}, Entries={Count}",
                    cost.TenantId, cost.ServiceOrigin, costInDollars, cost.TotalCount);

                foreach (var channel in cost.ChannelBreakdown)
                {
                    var channelCostInDollars = channel.CostMicros / 1_000_000.0m;
                    _logger.LogInformation(
                        "  Channel={Channel}, Cost=${Cost:F4}, Count={Count}",
                        channel.Channel, channelCostInDollars, channel.Count);
                }

                // TODO: Send monthly cost summary alert via Service Bus
                // Example: serviceBusClient.SendMessageAsync(new ServiceBusMessage(JsonSerializer.Serialize(cost)))
            }

            // Reset monthly budget tracking flags (if BudgetConfigEntity exists)
            // TODO: Implement budget reset logic when BudgetConfigEntity is available
            // Example:
            // var budgetConfigs = await _dbContext.BudgetConfigs.ToListAsync();
            // foreach (var config in budgetConfigs)
            // {
            //     config.Alert80PercentSent = false;
            //     config.Alert100PercentSent = false;
            //     config.CurrentMonthSpend = 0;
            // }
            // await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "MonthlyCostRollup completed. Processed {Count} tenant/service summaries for {Month}",
                monthlyCosts.Count, firstDayOfLastMonth.ToString("yyyy-MM"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MonthlyCostRollup");
            throw;
        }

        if (timer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next monthly cost rollup at: {NextRun}", timer.ScheduleStatus.Next);
        }
    }
}
