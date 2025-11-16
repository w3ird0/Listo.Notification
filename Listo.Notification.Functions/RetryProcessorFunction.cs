using Listo.Notification.Application.Interfaces;
using Listo.Notification.Application.Services;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Domain.Enums;
using Listo.Notification.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Functions;

/// <summary>
/// Processes NotificationQueue entries that are due for retry.
/// Timer interval configurable via RetryProcessor:Schedule (default: 1 minute).
/// NOTE: This is a placeholder implementation. Full retry processing requires:
/// - Integration with Service Bus queue processing
/// - NotificationQueueEntity to track status and correlation with NotificationEntity
/// - Provider failure handling and retry state management
/// Runs as singleton to prevent concurrent processing.
/// </summary>
public class RetryProcessorFunction
{
    private readonly ILogger<RetryProcessorFunction> _logger;
    private readonly NotificationDbContext _dbContext;
    private readonly ExponentialBackoffRetryService _retryService;
    private readonly ISmsProvider _smsProvider;
    private readonly IEmailProvider _emailProvider;
    private readonly IPushProvider _pushProvider;
    private readonly IConfiguration _configuration;

    public RetryProcessorFunction(
        ILogger<RetryProcessorFunction> logger,
        NotificationDbContext dbContext,
        ExponentialBackoffRetryService retryService,
        ISmsProvider smsProvider,
        IEmailProvider emailProvider,
        IPushProvider pushProvider,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _retryService = retryService ?? throw new ArgumentNullException(nameof(retryService));
        _smsProvider = smsProvider ?? throw new ArgumentNullException(nameof(smsProvider));
        _emailProvider = emailProvider ?? throw new ArgumentNullException(nameof(emailProvider));
        _pushProvider = pushProvider ?? throw new ArgumentNullException(nameof(pushProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Timer-triggered function for processing retry queue.
    /// Timer interval: Configurable via RetryProcessor:IntervalMinutes (default: 1 minute).
    /// CRON format: "0 */{IntervalMinutes} * * * *"
    /// Singleton mode: IsSingleton = true to prevent concurrent execution.
    /// </summary>
    [Function("RetryProcessor")]
    [Singleton]
    public async Task RunAsync(
        [TimerTrigger("%RetryProcessor:Schedule%", RunOnStartup = false)] TimerInfo timer,
        FunctionContext context)
    {
        _logger.LogInformation("RetryProcessor started at {Time}", DateTime.UtcNow);

        try
        {
            var batchSize = _configuration.GetValue<int>("RetryProcessor:BatchSize", 100);
            var now = DateTime.UtcNow;

            // Query NotificationQueue entries due for retry (not deleted, NextAttemptAt <= now, Attempts < max)
            var maxAttempts = _configuration.GetValue<int>("RetryProcessor:MaxAttempts", 5);
            var retryItems = await _dbContext.NotificationQueue
                .Where(q => !q.IsDeleted 
                    && q.NextAttemptAt != null
                    && q.NextAttemptAt <= now 
                    && q.Attempts < maxAttempts)
                .OrderBy(q => q.NextAttemptAt)
                .Take(batchSize)
                .ToListAsync();

            _logger.LogInformation("Found {Count} queue entries due for retry (batch size: {BatchSize})", 
                retryItems.Count, batchSize);

            // TODO: Implement retry processing logic
            // For each retry item:
            // 1. Decrypt PII (email, phone, FCM token) using crypto service
            // 2. Fetch or reconstruct notification request from PayloadJson
            // 3. Route to appropriate provider based on Channel
            // 4. Update Attempts, NextAttemptAt, LastErrorCode, LastErrorMessage
            // 5. If success, remove from queue or mark completed
            // 6. If exhausted, move to dead-letter or mark as permanently failed

            _logger.LogInformation(
                "RetryProcessor completed (placeholder). Found {Count} retry candidates",
                retryItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RetryProcessor");
            throw;
        }

        if (timer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next retry processor run at: {NextRun}", timer.ScheduleStatus.Next);
        }
    }
}

/// <summary>
/// Attribute to mark Azure Function as singleton (only one instance runs at a time).
/// Prevents concurrent execution of timer-triggered functions.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SingletonAttribute : Attribute
{
    public string? LockId { get; set; }
    public string? Scope { get; set; }
}
