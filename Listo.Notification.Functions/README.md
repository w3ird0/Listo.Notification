# Listo.Notification.Functions

Azure Functions project for background processing and webhook handling in the Listo Notification Service.

## Overview

This project contains serverless Azure Functions for:
- Scheduled notification processing
- Provider webhook handling (FCM, Twilio, SendGrid)
- Background tasks and batch operations

## Functions

### 1. ScheduledNotificationProcessor
- **Trigger**: Timer (CRON: `0 */1 * * * *` - every minute)
- **Purpose**: Process scheduled notifications that need to be sent
- **Authentication**: Not required (internal timer trigger)

### 2. WebhookProcessor
- **Trigger**: HTTP POST
- **Route**: `api/webhooks/{provider}`
- **Purpose**: Handle webhooks from notification providers
- **Authentication**: Function key required
- **Supported Providers**: FCM, Twilio, SendGrid

## Configuration

### local.settings.json
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ConnectionStrings__NotificationDb": "Server=localhost;Database=ListoNotification;Trusted_Connection=True;",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "<your-app-insights-connection-string>"
  }
}
```

### host.json
The `host.json` file configures:
- Function timeout (5 minutes)
- Logging levels
- HTTP route prefix (`api`)

## Development

### Prerequisites
- .NET 9.0 SDK
- Azure Functions Core Tools v4
- Azure Storage Emulator or Azurite (for local development)

### Running Locally
```bash
cd src/Listo.Notification.Functions
func start
```

### Building
```bash
dotnet build
```

### Testing Functions Locally

#### Test Timer Function (automatically runs every minute)
The timer function runs automatically based on the configured schedule.

#### Test Webhook Function
```bash
# FCM Webhook
curl -X POST http://localhost:7071/api/webhooks/fcm \
  -H "Content-Type: application/json" \
  -d '{"messageId":"test123","deliveryStatus":"delivered"}'

# Twilio Webhook
curl -X POST http://localhost:7071/api/webhooks/twilio \
  -H "Content-Type: application/json" \
  -d '{"MessageSid":"SM123","MessageStatus":"delivered"}'

# SendGrid Webhook
curl -X POST http://localhost:7071/api/webhooks/sendgrid \
  -H "Content-Type: application/json" \
  -d '[{"event":"delivered","email":"test@example.com"}]'
```

## Deployment

### Deploy to Azure
```bash
func azure functionapp publish <FunctionAppName>
```

### Environment Variables (Azure)
Configure the following application settings in Azure:
- `ConnectionStrings__NotificationDb`: Database connection string
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: Application Insights connection string
- Additional provider-specific settings (API keys, secrets, etc.)

## Architecture

The Functions project follows these principles:
- **Isolated Worker Process**: Uses .NET 9 isolated worker for better performance and independence
- **Dependency Injection**: Configured in `Program.cs`
- **Logging**: Structured logging to Application Insights
- **Error Handling**: Comprehensive try-catch blocks with appropriate HTTP status codes

## Integration with Main API

The Functions project shares:
- Domain models (via `Listo.Notification.Domain`)
- Application services (via `Listo.Notification.Application`)
- Infrastructure components (via `Listo.Notification.Infrastructure`)

This ensures consistency across the API and background processing.

## Security

- Function keys are required for HTTP-triggered functions
- Webhook endpoints validate provider signatures (to be implemented)
- All sensitive configuration is stored in Azure Key Vault (production)

## Monitoring

- All functions log to Application Insights
- Key metrics tracked:
  - Function execution duration
  - Success/failure rates
  - Webhook processing statistics
  - Scheduled notification processing throughput

## TODO / Future Enhancements

- [ ] Implement provider-specific webhook parsing and validation
- [ ] Add signature validation for webhooks
- [ ] Implement delivery receipt updates
- [ ] Add batch processing for large notification volumes
- [ ] Add more scheduled functions (e.g., cleanup, reporting)
- [ ] Implement retry logic for failed notifications
- [ ] Add dead-letter queue processing
- [ ] Add integration tests for Functions
