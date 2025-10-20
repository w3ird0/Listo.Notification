# Section 14: Azure Functions Configuration

## Overview

The Listo.Notification service uses Azure Functions for background jobs and scheduled processing:
- `ScheduledNotificationProcessor`: Processes DB-scheduled notifications
- `RetryProcessorFunction`: Retries failed notifications with exponential backoff
- `BudgetMonitorFunction`: Monitors budget thresholds and sends alerts
- `DailyCostAggregatorFunction`: Aggregates daily costs for reporting
- `MonthlyCostRollupFunction`: Performs monthly cost rollup and budget reset
- `DataRetentionCleanerFunction`: Soft-deletes old data per retention policies

All functions use singleton concurrency control to prevent overlapping executions.

## Timer Configuration

All timer triggers use NCRONTAB expressions and are configurable via environment variables.

### NCRONTAB Format
```
{second} {minute} {hour} {day} {month} {day of week}
```

### Default Schedules

| Function | Schedule | Frequency | Config Key |
|----------|----------|-----------|------------|
| ScheduledNotificationProcessor | `0 */1 * * * *` | Every minute | `ScheduledNotificationProcessor__Schedule` |
| RetryProcessor | `0 */1 * * * *` | Every minute | `RetryProcessor__Schedule` |
| BudgetMonitor | `0 0 * * * *` | Every hour | `BudgetMonitor__Schedule` |
| DailyCostAggregator | `0 0 0 * * *` | Midnight UTC daily | `DailyCostAggregator__Schedule` |
| MonthlyCostRollup | `0 0 2 1 * *` | 2 AM UTC on 1st of month | `MonthlyCostRollup__Schedule` |
| DataRetentionCleaner | `0 0 3 * * 0` | 3 AM UTC every Sunday | `DataRetentionCleaner__Schedule` |

## Local Development Setup

### Prerequisites
- .NET 9 SDK
- Azure Functions Core Tools v4+
- Azure Storage Emulator or Azurite
- SQL Server or SQL Server Express

### Configuration Files

#### 1. `local.settings.json`
Copy `local.settings.example.json` to `local.settings.json` and configure:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    
    "ConnectionStrings__NotificationDb": "Server=localhost;Database=ListoNotification;Trusted_Connection=True;",
    
    "ScheduledNotificationProcessor__Schedule": "0 */1 * * * *",
    "RetryProcessor__Schedule": "0 */1 * * * *",
    "RetryProcessor__BatchSize": "100",
    "RetryProcessor__MaxAttempts": "5",
    
    "BudgetMonitor__Schedule": "0 0 * * * *",
    "Budget__Threshold80Percent": "0.8",
    "Budget__Threshold100Percent": "1.0",
    
    "DailyCostAggregator__Schedule": "0 0 0 * * *",
    "MonthlyCostRollup__Schedule": "0 0 2 1 * *",
    
    "DataRetentionCleaner__Schedule": "0 0 3 * * 0",
    "DataRetention__NotificationDays": "90",
    "DataRetention__MessageDays": "90",
    "DataRetention__AuditLogDays": "365",
    "DataRetention__QueueDays": "30",
    "DataRetention__SafetyLimit": "10000"
  }
}
```

**IMPORTANT**: `local.settings.json` is excluded from git. Never commit secrets.

#### 2. `host.json`
Configured with:
- Function timeout: 10 minutes
- Singleton lock configuration for concurrency control
- Application Insights telemetry sampling

```json
{
  "version": "2.0",
  "functionTimeout": "00:10:00",
  "singleton": {
    "listenerLockPeriod": "00:01:00",
    "lockPeriod": "00:00:15",
    "lockAcquisitionTimeout": "00:05:00"
  }
}
```

### Running Locally

1. Start Azurite (Azure Storage Emulator):
   ```bash
   azurite --silent
   ```

2. Ensure SQL Server is running with `ListoNotification` database migrated

3. Run functions:
   ```bash
   cd src/Listo.Notification.Functions
   func start
   ```

4. Monitor output for timer triggers and execution logs

### Testing Individual Functions

Manually trigger a function without waiting for timer:
```bash
# Trigger ScheduledNotificationProcessor
func azure functionapp publish <function-app-name> --publish-local-settings
```

Or use HTTP trigger endpoints (if configured) for testing.

## Function Details

### 1. ScheduledNotificationProcessor

**Purpose**: Processes notifications with scheduled delivery times from the database.

**Behavior**:
- Queries `Notifications` table for records where `ScheduledAt <= Now`
- Routes to appropriate provider (SMS/Email/Push)
- Updates notification status (`Sent`, `Failed`)
- Logs provider message ID and error details

**Configuration**:
- `ScheduledNotificationProcessor__Schedule`: NCRONTAB expression (default: every minute)

**Concurrency**: Singleton (prevents overlapping executions)

**Notes**:
- Only processes DB-scheduled notifications (not queue-based)
- Synchronous delivery is handled by API project, not this function
- Function has read/write access to NotificationEntity

---

### 2. RetryProcessorFunction

**Purpose**: Retries failed notifications from the `NotificationQueue` table.

**Status**: Placeholder implementation. Full retry processing requires:
- Integration with Service Bus queue processing
- Decryption of PII (email, phone, FCM token) from queue entries
- Provider failure handling and retry state management

**Behavior** (Planned):
- Queries `NotificationQueue` where `NextAttemptAt <= Now` and `Attempts < MaxAttempts`
- Decrypts PII using crypto service
- Routes to appropriate provider based on `Channel`
- Updates `Attempts`, `NextAttemptAt`, `LastErrorCode`, `LastErrorMessage`
- On success: removes from queue or marks completed
- On exhaustion: moves to dead-letter or marks permanently failed

**Configuration**:
- `RetryProcessor__Schedule`: NCRONTAB expression (default: every minute)
- `RetryProcessor__BatchSize`: Max entries to process per execution (default: 100)
- `RetryProcessor__MaxAttempts`: Maximum retry attempts before giving up (default: 5)

**Concurrency**: Singleton

**Dependencies**:
- `ExponentialBackoffRetryService` for backoff calculation
- SMS/Email/Push providers for delivery
- Crypto service for PII decryption (TODO)

---

### 3. BudgetMonitorFunction

**Purpose**: Monitors budget utilization hourly and sends alerts at configurable thresholds.

**Behavior**:
- Queries `CostTracking` table for current month spend
- Groups by `TenantId` and `ServiceOrigin`
- Computes total cost from `TotalCostMicros` (divided by 1,000,000 for dollars)
- Compares against budget thresholds (80%, 100%)
- Logs current spend per tenant/service/channel

**Planned Enhancements** (Requires `BudgetConfigEntity`):
- Query budget limits from `BudgetConfigEntity`
- Track alert flags (`Alert80PercentSent`, `Alert100PercentSent`)
- Send alerts via Service Bus when thresholds exceeded
- Send email alerts to tenant admins

**Configuration**:
- `BudgetMonitor__Schedule`: NCRONTAB expression (default: hourly)
- `Budget__Threshold80Percent`: 80% alert threshold (default: 0.8)
- `Budget__Threshold100Percent`: 100% alert threshold (default: 1.0)

**Concurrency**: Singleton

**Cost Calculation**: Sums `TotalCostMicros` from `CostTracking` and converts to dollars.

---

### 4. DailyCostAggregatorFunction

**Purpose**: Aggregates daily notification costs from raw cost tracking data.

**Behavior**:
- Runs daily at midnight UTC (configurable)
- Queries `CostTracking` entries from previous day (`OccurredAt` column)
- Groups by `TenantId`, `ServiceOrigin`, `Channel`
- Sums `TotalCostMicros` and converts to dollars for logging
- Logs daily cost summary per tenant/service/channel

**Notes**:
- `CostTrackingEntity` is per-message, not daily aggregate
- This function computes aggregates from existing entries for reporting
- Does not create new CostTracking entries (avoids duplicates)
- For long-term storage of daily aggregates, consider separate `DailyCostSummary` table

**Configuration**:
- `DailyCostAggregator__Schedule`: NCRONTAB expression (default: `0 0 0 * * *` = midnight UTC)

**Concurrency**: Singleton

---

### 5. MonthlyCostRollupFunction

**Purpose**: Performs monthly cost rollup and budget reset on 1st of each month.

**Behavior**:
- Runs on 1st of month at 2 AM UTC (configurable)
- Queries `CostTracking` entries for previous month
- Groups by `TenantId`, `ServiceOrigin`, and `Channel`
- Sums costs and logs monthly summary
- Logs channel breakdown (SMS, Email, Push, In-App)

**Planned Enhancements** (Requires `BudgetConfigEntity`):
- Reset monthly budget tracking flags (`Alert80PercentSent`, `Alert100PercentSent`)
- Reset `CurrentMonthSpend` counters
- Send monthly cost summary alerts via Service Bus
- Email monthly reports to tenant admins

**Configuration**:
- `MonthlyCostRollup__Schedule`: NCRONTAB expression (default: `0 0 2 1 * *` = 2 AM UTC on 1st of month)

**Concurrency**: Singleton

**Cost Calculation**: Sums `TotalCostMicros` from `CostTracking` and converts to dollars.

---

### 6. DataRetentionCleanerFunction

**Purpose**: Soft-deletes old records per data retention policies for compliance (GDPR, HIPAA).

**Behavior**:
- Runs weekly on Sunday at 3 AM UTC (configurable)
- Soft-deletes records older than configured retention periods:
  - `Notifications`: 90 days (default)
  - `Messages`: 90 days (default)
  - `AuditLogs`: 365 days (default)
  - `NotificationQueue`: 30 days (default)
- Sets `IsDeleted = true` and `DeletedAt = Now` (soft delete)
- Implements safety limit (max 10,000 records per execution by default)
- Logs cleanup statistics

**Configuration**:
- `DataRetentionCleaner__Schedule`: NCRONTAB expression (default: `0 0 3 * * 0` = Sunday 3 AM UTC)
- `DataRetention__NotificationDays`: Retention period for notifications (default: 90)
- `DataRetention__MessageDays`: Retention period for messages (default: 90)
- `DataRetention__AuditLogDays`: Retention period for audit logs (default: 365)
- `DataRetention__QueueDays`: Retention period for queue entries (default: 30)
- `DataRetention__SafetyLimit`: Max records to delete per execution (default: 10000)

**Concurrency**: Singleton

**Safety**: Uses safety limit to prevent accidental mass deletions. If more records need deletion, function will process them in subsequent runs.

**Criteria**:
- `Notifications`: `CreatedAt < cutoff && !IsDeleted`
- `Messages`: `SentAt < cutoff && !IsDeleted`
- `AuditLogs`: `OccurredAt < cutoff && !IsDeleted`
- `NotificationQueue`: `CreatedAt < cutoff && !IsDeleted`

---

## Concurrency Control (Singleton Pattern)

All functions use the `[Singleton]` attribute to ensure only one instance runs at a time. This prevents:
- Race conditions when updating database records
- Duplicate processing of scheduled/retry items
- Budget alert spam (multiple alerts for same threshold)

### Singleton Configuration in `host.json`

```json
"singleton": {
  "listenerLockPeriod": "00:01:00",
  "listenerLockRecoveryPollingInterval": "00:01:00",
  "lockPeriod": "00:00:15",
  "lockAcquisitionTimeout": "00:05:00",
  "lockAcquisitionPollingInterval": "00:00:05"
}
```

- **listenerLockPeriod**: How long a function holds the listener lock
- **lockPeriod**: Lock duration for singleton scope
- **lockAcquisitionTimeout**: Max time to wait for lock acquisition
- **lockAcquisitionPollingInterval**: Polling interval when waiting for lock

## Deployment

### Azure Configuration

#### Application Settings (Environment Variables)
Configure in Azure Portal → Function App → Configuration → Application settings:

```
AzureWebJobsStorage=<storage-account-connection-string>
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
APPLICATIONINSIGHTS_CONNECTION_STRING=<app-insights-connection-string>

ConnectionStrings__NotificationDb=@Microsoft.KeyVault(SecretUri=https://your-keyvault.vault.azure.net/secrets/NotificationDbConnectionString/)

ScheduledNotificationProcessor__Schedule=0 */1 * * * *
RetryProcessor__Schedule=0 */1 * * * *
RetryProcessor__BatchSize=100
RetryProcessor__MaxAttempts=5

BudgetMonitor__Schedule=0 0 * * * *
Budget__Threshold80Percent=0.8
Budget__Threshold100Percent=1.0

DailyCostAggregator__Schedule=0 0 0 * * *
MonthlyCostRollup__Schedule=0 0 2 1 * *

DataRetentionCleaner__Schedule=0 0 3 * * 0
DataRetention__NotificationDays=90
DataRetention__MessageDays=90
DataRetention__AuditLogDays=365
DataRetention__QueueDays=30
DataRetention__SafetyLimit=10000
```

#### Key Vault Integration
Use Key Vault references for secrets:
```
ConnectionStrings__NotificationDb=@Microsoft.KeyVault(SecretUri=https://<vault-name>.vault.azure.net/secrets/NotificationDbConnectionString/)
ServiceBus__ConnectionString=@Microsoft.KeyVault(SecretUri=https://<vault-name>.vault.azure.net/secrets/ServiceBusConnectionString/)
```

### Deployment Methods

#### 1. Visual Studio Code
- Install Azure Functions extension
- Right-click Functions project → Deploy to Function App
- Select subscription and function app

#### 2. Azure CLI
```bash
# Build and publish
dotnet publish src/Listo.Notification.Functions -c Release -o ./publish

# Deploy
cd ./publish
func azure functionapp publish <function-app-name>
```

#### 3. Azure DevOps / GitHub Actions
See CI/CD pipeline documentation (planned).

### Scaling Configuration

- **Consumption Plan**: Auto-scales based on load
- **Premium Plan**: Pre-warmed instances, VNet integration, longer execution time
- **App Service Plan**: Dedicated compute for predictable costs

For singleton functions, scaling is controlled by lock acquisition. Even with multiple instances, only one executes the function at a time.

## Monitoring

### Application Insights
All functions log to Application Insights automatically:

**Key Metrics to Monitor**:
- Function execution duration
- Function invocation count
- Function failure rate
- Custom metrics: cost aggregations, retry attempts, cleanup stats

**Queries** (Kusto Query Language):
```kql
// Function execution times
traces
| where customDimensions.Category == "Function"
| summarize avg(duration), max(duration), count() by operation_Name
| order by avg_duration desc

// Errors in last 24 hours
exceptions
| where timestamp > ago(24h)
| summarize count() by type, outerMessage

// RetryProcessor stats
traces
| where message contains "RetryProcessor"
| order by timestamp desc
| take 100
```

### Azure Portal Monitoring
Navigate to Function App → Monitor:
- Function execution history
- Logs (Live Metrics Stream)
- Metrics: Execution count, execution units, errors

### Alerts
Configure alerts for:
- Function execution failures (threshold: >5 in 5 minutes)
- Function timeout (execution duration >5 minutes)
- Budget threshold alerts (custom metric from BudgetMonitor)

## Error Handling and Retry

### Automatic Retries (Timer Triggers)
Azure Functions automatically retries timer-triggered functions on failure:
- Max retries: 5 (default)
- Exponential backoff between retries

### Dead-Letter Handling (Service Bus Triggers)
For Service Bus triggered functions (planned):
- Configure dead-letter queues for failed messages
- Max delivery count: 10 (configurable)
- Move to dead-letter after exhaustion

### Custom Retry Logic
`RetryProcessorFunction` implements custom retry with `ExponentialBackoffRetryService`:
- Configurable base delay, backoff factor, jitter, max attempts
- Per-channel retry policies from `RetryPolicies` table

## Best Practices

1. **Use Key Vault for Secrets**: Never store connection strings in Application Settings directly
2. **Monitor Function Health**: Set up alerts for failures and long execution times
3. **Test Locally First**: Always test timer schedules and logic locally before deploying
4. **Singleton for Stateful Operations**: Use singleton pattern for database writes to prevent race conditions
5. **Idempotency**: Design functions to be idempotent (safe to run multiple times)
6. **Safety Limits**: Use batch size limits and safety thresholds (e.g., DataRetentionCleaner)
7. **Structured Logging**: Use structured logging with correlation IDs for traceability
8. **Cost Awareness**: Understand Consumption Plan pricing (per execution) vs Premium/App Service Plan

## Troubleshooting

### Function Not Triggering

**Symptoms**: Timer function doesn't execute at expected time

**Diagnosis**:
1. Check NCRONTAB expression syntax
2. Verify Application Settings in Azure Portal
3. Check Function App → Functions → <FunctionName> → Monitor for execution history
4. Review Application Insights logs for errors

**Solutions**:
- Validate NCRONTAB expression: https://crontab.guru/ (convert to NCRONTAB format)
- Restart Function App
- Check if function is disabled in portal
- Verify `AzureWebJobsStorage` is configured correctly

### Singleton Lock Timeout

**Symptoms**: Function logs "Failed to acquire lock" or times out

**Diagnosis**:
1. Check if another instance is holding the lock
2. Review `host.json` singleton configuration
3. Check for long-running operations blocking the lock

**Solutions**:
- Increase `lockAcquisitionTimeout` in `host.json`
- Optimize function execution time (reduce batch sizes)
- Check for deadlocks or stuck function instances
- Restart Function App to release stale locks

### Database Connection Failures

**Symptoms**: Function fails with SQL timeout or connection errors

**Diagnosis**:
1. Verify connection string in Key Vault / Application Settings
2. Check SQL Server firewall rules (allow Azure services)
3. Review Application Insights exceptions

**Solutions**:
- Add Function App outbound IP to SQL Server firewall
- Use Managed Identity for SQL authentication (recommended)
- Enable VNet integration (Premium/App Service Plan)
- Check database DTU/CPU usage

### High Execution Costs

**Symptoms**: Unexpectedly high Azure Functions bill

**Diagnosis**:
1. Check Function App metrics (execution count, execution units)
2. Review function schedules (too frequent?)
3. Check for retry loops

**Solutions**:
- Adjust timer schedules (reduce frequency if possible)
- Optimize batch sizes to reduce execution time
- Consider Premium Plan for predictable costs
- Review retry policies (avoid infinite retries)

## Future Enhancements

1. **Service Bus Integration**: Implement Service Bus queue/topic triggered functions for event-driven processing
2. **BudgetConfigEntity**: Add budget configuration table and integrate with BudgetMonitorFunction
3. **Dead-Letter Processing**: Add function to process dead-letter queue entries
4. **Webhook Processing**: Add function to handle FCM delivery receipts and update notification status
5. **CI/CD Pipeline**: Automate deployment with GitHub Actions or Azure DevOps
6. **Health Checks**: Implement health check endpoint for monitoring function app health
7. **Durable Functions**: Consider Durable Functions for long-running workflows (e.g., bulk notification processing)

## References

- [Azure Functions Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)
- [NCRONTAB Expressions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=python-v2%2Cisolated-process%2Cnodejs-v4&pivots=programming-language-csharp#ncrontab-expressions)
- [Singleton Pattern in Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=python-v2%2Cisolated-process%2Cnodejs-v4&pivots=programming-language-csharp#singleton-pattern)
- [Key Vault References](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)
- [Application Insights for Functions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-monitoring)
