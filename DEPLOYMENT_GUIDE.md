# Listo.Notification Deployment and Configuration Guide

**Version:** 1.0  
**Last Updated:** 2025-01-20  
**Target Framework:** .NET 9.0

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Development Setup](#local-development-setup)
3. [Azure Resources Setup](#azure-resources-setup)
4. [Configuration](#configuration)
5. [Database Deployment](#database-deployment)
6. [API Deployment](#api-deployment)
7. [Azure Functions Deployment](#azure-functions-deployment)
8. [Provider Configuration](#provider-configuration)
9. [Monitoring and Logging](#monitoring-and-logging)
10. [Security Checklist](#security-checklist)
11. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Development Environment:
- .NET 9.0 SDK
- Visual Studio 2022 (17.8+) or VS Code
- SQL Server 2019+ or Azure SQL Database
- Azure Storage Emulator or Azurite
- Azure Functions Core Tools v4
- Redis (local or Azure Redis Cache)

### Azure Resources Required:
- Azure App Service (for API)
- Azure Functions App
- Azure SQL Database
- Azure Redis Cache
- Azure Service Bus (namespace, queues, topics)
- Azure SignalR Service
- Azure Application Insights
- Azure Key Vault
- Azure Storage Account

### External Service Accounts:
- Twilio account (SMS)
- SendGrid account (Email)
- Firebase project (Push notifications)

---

## Local Development Setup

### 1. Clone and Restore

```powershell
cd D:\OneDrive\Projects\ListoExpress\Dev\Listo.Notification
dotnet restore
```

### 2. Database Setup

```powershell
# Update connection string in appsettings.Development.json
# Run migrations
cd src\Listo.Notification.API
dotnet ef database update
```

### 3. Configure User Secrets (Development)

```powershell
# API Project
cd src\Listo.Notification.API
dotnet user-secrets init

# Add secrets
dotnet user-secrets set "Twilio:AccountSid" "your-twilio-sid"
dotnet user-secrets set "Twilio:AuthToken" "your-twilio-token"
dotnet user-secrets set "SendGrid:ApiKey" "your-sendgrid-key"
dotnet user-secrets set "FCM:ServerKey" "your-fcm-server-key"
```

### 4. Run Locally

```powershell
# Terminal 1: Run API
cd src\Listo.Notification.API
dotnet run

# Terminal 2: Run Functions
cd src\Listo.Notification.Functions
func start
```

---

## Azure Resources Setup

### 1. Resource Group

```bash
az group create \
  --name rg-listo-notification-prod \
  --location eastus
```

### 2. Azure SQL Database

```bash
az sql server create \
  --name sql-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --admin-user listodbadmin \
  --admin-password <SecurePassword>

az sql db create \
  --resource-group rg-listo-notification-prod \
  --server sql-listo-notification-prod \
  --name listonotification \
  --service-objective S1
```

### 3. Azure Redis Cache

```bash
az redis create \
  --name redis-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --sku Basic \
  --vm-size C1
```

### 4. Azure Service Bus

```bash
az servicebus namespace create \
  --name sb-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --sku Standard

# Create queues
az servicebus queue create \
  --namespace-name sb-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --name notification-requests

# Create topics
az servicebus topic create \
  --namespace-name sb-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --name notification-events
```

### 5. Azure SignalR Service

```bash
az signalr create \
  --name signalr-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --sku Standard_S1 \
  --unit-count 1 \
  --service-mode Serverless
```

### 6. Azure App Service

```bash
# Create App Service Plan
az appservice plan create \
  --name plan-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --sku P1V2 \
  --is-linux

# Create Web App
az webapp create \
  --name app-listo-notification-api-prod \
  --resource-group rg-listo-notification-prod \
  --plan plan-listo-notification-prod \
  --runtime "DOTNETCORE:9.0"
```

### 7. Azure Functions App

```bash
# Create Storage Account
az storage account create \
  --name stlistonotifprod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --sku Standard_LRS

# Create Function App
az functionapp create \
  --name func-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --storage-account stlistonotifprod \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 9.0 \
  --functions-version 4
```

### 8. Application Insights

```bash
az monitor app-insights component create \
  --app appi-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --application-type web
```

### 9. Key Vault

```bash
az keyvault create \
  --name kv-listo-notif-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus
```

---

## Configuration

### API - appsettings.json

```json
{
  "ConnectionStrings": {
    "NotificationDb": "Server=sql-listo-notification-prod.database.windows.net;Database=listonotification;",
    "Redis": "redis-listo-notification-prod.redis.cache.windows.net:6380,ssl=true"
  },
  "Jwt": {
    "Authority": "https://auth.listoexpress.com",
    "Audience": "listo-notification-api"
  },
  "RateLimiting": {
    "DefaultLimit": 100,
    "DefaultWindow": "00:01:00"
  },
  "Twilio": {
    "AccountSid": "",
    "AuthToken": "",
    "FromPhoneNumber": "+1234567890"
  },
  "SendGrid": {
    "ApiKey": "",
    "FromEmail": "noreply@listoexpress.com",
    "FromName": "Listo Express"
  },
  "FCM": {
    "ProjectId": "your-firebase-project",
    "ServerKey": ""
  }
}
```

### Azure App Service Configuration

Set application settings in Azure Portal or via CLI:

```bash
az webapp config appsettings set \
  --name app-listo-notification-api-prod \
  --resource-group rg-listo-notification-prod \
  --settings \
    "ConnectionStrings__NotificationDb=@Microsoft.KeyVault(SecretUri=https://kv-listo-notif-prod.vault.azure.net/secrets/NotificationDbConnectionString/)" \
    "Twilio__AccountSid=@Microsoft.KeyVault(SecretUri=https://kv-listo-notif-prod.vault.azure.net/secrets/TwilioAccountSid/)" \
    "SendGrid__ApiKey=@Microsoft.KeyVault(SecretUri=https://kv-listo-notif-prod.vault.azure.net/secrets/SendGridApiKey/)"
```

### Azure Functions Configuration

```bash
az functionapp config appsettings set \
  --name func-listo-notification-prod \
  --resource-group rg-listo-notification-prod \
  --settings \
    "ConnectionStrings__NotificationDb=@Microsoft.KeyVault(SecretUri=https://kv-listo-notif-prod.vault.azure.net/secrets/NotificationDbConnectionString/)" \
    "Twilio__AccountSid=@Microsoft.KeyVault(SecretUri=https://kv-listo-notif-prod.vault.azure.net/secrets/TwilioAccountSid/)"
```

---

## Database Deployment

### 1. Create Migration (if needed)

```powershell
cd src\Listo.Notification.API
dotnet ef migrations add InitialCreate
```

### 2. Generate SQL Script

```powershell
dotnet ef migrations script --output migration.sql --idempotent
```

### 3. Apply to Azure SQL

```bash
sqlcmd -S sql-listo-notification-prod.database.windows.net \
  -d listonotification \
  -U listodbadmin \
  -P <Password> \
  -i migration.sql
```

Or use EF Core tools to update directly:

```powershell
dotnet ef database update --connection "Server=sql-listo-notification-prod.database.windows.net;..."
```

---

## API Deployment

### Option 1: GitHub Actions (Recommended)

See `.github/workflows/deploy-api.yml` in repository.

### Option 2: Manual Deployment

```powershell
# Build and publish
cd src\Listo.Notification.API
dotnet publish -c Release -o ./publish

# Deploy to Azure
az webapp deployment source config-zip \
  --name app-listo-notification-api-prod \
  --resource-group rg-listo-notification-prod \
  --src ./publish.zip
```

### Post-Deployment Steps:

1. **Verify Health Check**
   ```bash
   curl https://app-listo-notification-api-prod.azurewebsites.net/health
   ```

2. **Run Smoke Tests**
   ```bash
   curl https://app-listo-notification-api-prod.azurewebsites.net/api/v1/notifications/health
   ```

3. **Check Application Insights**
   - Verify telemetry is flowing
   - Check for startup errors

---

## Azure Functions Deployment

### Deploy Functions

```powershell
cd src\Listo.Notification.Functions
func azure functionapp publish func-listo-notification-prod
```

### Verify Functions

```bash
# List functions
az functionapp function list \
  --name func-listo-notification-prod \
  --resource-group rg-listo-notification-prod

# Check logs
az functionapp log tail \
  --name func-listo-notification-prod \
  --resource-group rg-listo-notification-prod
```

---

## Provider Configuration

### Twilio Setup

1. Create account at https://www.twilio.com/
2. Get Account SID and Auth Token
3. Purchase phone number
4. Configure webhook URL in Twilio console:
   - `https://func-listo-notification-prod.azurewebsites.net/api/webhooks/twilio`

### SendGrid Setup

1. Create account at https://sendgrid.com/
2. Generate API key with "Mail Send" permission
3. Verify sender email/domain
4. Configure Event Webhook:
   - `https://func-listo-notification-prod.azurewebsites.net/api/webhooks/sendgrid`

### Firebase Cloud Messaging Setup

1. Create Firebase project at https://console.firebase.google.com/
2. Generate service account key
3. Get Server Key from Project Settings
4. Configure FCM webhook (if needed)

---

## Monitoring and Logging

### Application Insights Configuration

```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "<key>",
    "EnableAdaptiveSampling": true,
    "EnablePerformanceCounterCollectionModule": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    },
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

### Key Metrics to Monitor

- **API Metrics:**
  - Request rate
  - Response time (P50, P95, P99)
  - Error rate
  - Rate limiting rejections

- **Function Metrics:**
  - Execution count
  - Success/failure rate
  - Duration
  - Scheduled notifications processed

- **Provider Metrics:**
  - Circuit breaker status
  - Delivery success rate
  - Provider response times

### Alerts to Configure

1. API Response Time > 5 seconds
2. Error Rate > 5%
3. Database CPU > 80%
4. Redis Memory > 90%
5. Function Execution Failures > 10 per hour

---

## Security Checklist

### Pre-Deployment:
- [ ] All secrets stored in Azure Key Vault
- [ ] Database connection uses Managed Identity
- [ ] SQL firewall configured (Azure IPs only)
- [ ] Redis requires SSL
- [ ] API requires HTTPS
- [ ] CORS configured for known origins only
- [ ] Rate limiting enabled
- [ ] JWT validation configured
- [ ] Service-to-service auth uses secrets

### Post-Deployment:
- [ ] Rotate all default passwords
- [ ] Enable Azure Defender
- [ ] Configure network security groups
- [ ] Enable diagnostic logs
- [ ] Set up backup policies
- [ ] Document disaster recovery plan
- [ ] Review IAM permissions

---

## Troubleshooting

### API Not Starting

**Check:**
1. Application Insights logs
2. Connection strings are correct
3. Database is accessible
4. Redis is accessible

**Commands:**
```bash
az webapp log tail --name app-listo-notification-api-prod --resource-group rg-listo-notification-prod
```

### Functions Not Triggering

**Check:**
1. Timer trigger CRON expression
2. Storage account connection
3. Application settings
4. Function app logs

**Commands:**
```bash
az functionapp log tail --name func-listo-notification-prod --resource-group rg-listo-notification-prod
```

### Provider Failures

**Check:**
1. Provider credentials in Key Vault
2. Provider API quotas
3. Network connectivity
4. Circuit breaker status

**Logs:**
- Search Application Insights for "Circuit breaker"
- Check provider-specific error messages

### Database Connection Issues

**Check:**
1. Firewall rules include Azure IPs
2. Connection string is correct
3. Managed Identity has permissions
4. Database is online

**Commands:**
```bash
az sql db show --name listonotification --server sql-listo-notification-prod --resource-group rg-listo-notification-prod
```

---

## Scaling Considerations

### API Scaling

- **Vertical:** Upgrade App Service Plan (P1V2 → P2V2 → P3V2)
- **Horizontal:** Enable auto-scale rules based on CPU/Memory
- **Recommended:** Min 2 instances for HA, max 10 for cost control

### Functions Scaling

- **Consumption Plan:** Auto-scales based on load
- **Premium Plan:** Pre-warmed instances, VNET integration
- **Dedicated Plan:** Predictable costs, guaranteed resources

### Database Scaling

- **DTU-based:** S1 (20 DTU) → S2 (50 DTU) → S3 (100 DTU)
- **vCore-based:** 2 vCore → 4 vCore → 8 vCore
- **Read replicas:** For read-heavy workloads

### Redis Scaling

- **Basic:** C0 → C1 → C2
- **Standard:** C0-C6 with HA
- **Premium:** P1-P5 with clustering, persistence

---

## Support and Maintenance

### Regular Tasks

**Daily:**
- Review error logs
- Check provider status
- Monitor queue depths

**Weekly:**
- Review performance metrics
- Check cost reports
- Update dependencies

**Monthly:**
- Security updates
- Capacity planning review
- Disaster recovery test

### Contact Information

- **Team:** Listo Notification Team
- **On-Call:** [PagerDuty/Rotation Link]
- **Documentation:** [Wiki Link]
- **Repository:** https://github.com/listoexpress/listo-notification

---

**Document Version:** 1.0  
**Last Reviewed:** 2025-01-20  
**Next Review:** 2025-04-20
