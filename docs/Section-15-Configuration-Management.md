# Section 15: Configuration Management

## Overview

This document describes the complete configuration management strategy for Listo.Notification service, including:

- **Configuration Options Classes** - Strongly-typed configuration with data annotations
- **Azure Key Vault Integration** - Secure secrets management
- **Environment-Specific Settings** - Development, Staging, Production
- **Feature Flags** - Runtime feature toggles
- **Startup Validation** - Fail-fast configuration validation

---

## Table of Contents

1. [Configuration Architecture](#configuration-architecture)
2. [Configuration Options Classes](#configuration-options-classes)
3. [Azure Key Vault Integration](#azure-key-vault-integration)
4. [Environment-Specific Configuration](#environment-specific-configuration)
5. [Feature Flags](#feature-flags)
6. [Startup Validation](#startup-validation)
7. [Configuration Loading Order](#configuration-loading-order)
8. [Best Practices](#best-practices)

---

## 1. Configuration Architecture

### Configuration Layers

```
┌──────────────────────────────────────────────┐
│   Environment Variables (Highest Priority)   │
├──────────────────────────────────────────────┤
│   appsettings.{Environment}.json             │
├──────────────────────────────────────────────┤
│   appsettings.json (Defaults)                │
├──────────────────────────────────────────────┤
│   Azure Key Vault (via @Microsoft.KeyVault)  │
└──────────────────────────────────────────────┘
```

### Configuration Structure

All configuration options are organized into logical sections:

- **Notification** - General service configuration
- **Azure** - All Azure service configurations
  - ServiceBus
  - SignalR
  - BlobStorage
  - KeyVault
  - ApplicationInsights
- **Redis** - Redis connection and caching
- **RateLimiting** - Rate limiting configuration
- **CostManagement** - Budget and cost tracking
- **FeatureFlags** - Feature toggles
- **Twilio, SendGrid, FCM** - Notification provider configuration
- **JwtSettings** - JWT authentication configuration

---

## 2. Configuration Options Classes

### File Structure

```
Infrastructure/Configuration/
├── NotificationOptions.cs          # General service configuration
├── AzureOptions.cs                 # All Azure service options
│   ├── ServiceBusOptions
│   ├── SignalROptions
│   ├── BlobStorageOptions
│   ├── KeyVaultOptions
│   └── ApplicationInsightsOptions
├── RedisOptions.cs                 # Redis and caching
│   ├── RedisOptions
│   ├── RateLimitingOptions
│   └── CostManagementOptions
├── FeatureFlags.cs                 # Feature flag toggles
└── ConfigurationValidator.cs       # Startup validation
```

### Example: NotificationOptions

```csharp
public class NotificationOptions
{
    public const string SectionName = "Notification";

    [Required]
    public string ServiceName { get; set; } = "Listo.Notification";

    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 90;

    [Range(1, 1000)]
    public int MaxBatchSize { get; set; } = 100;

    public bool EnableRealTimeDelivery { get; set; } = true;
    public bool EnableAsyncProcessing { get; set; } = true;
    
    [Range(1, 104857600)]
    public long MaxAttachmentSizeBytes { get; set; } = 10485760; // 10MB
    
    public List<string> AllowedAttachmentTypes { get; set; } = new()
    {
        "image/jpeg", "image/png", "image/gif",
        "application/pdf", "text/plain"
    };
}
```

### Key Features

- **Data Annotations** - Validation attributes for ranges, requirements
- **Const Section Names** - Strongly-typed section references
- **Sensible Defaults** - All properties have default values
- **Documentation** - XML comments for all properties

---

## 3. Azure Key Vault Integration

### Configuration in appsettings.Production.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "@Microsoft.KeyVault(SecretUri=https://listo-kv.vault.azure.net/secrets/NotificationDb-ConnectionString/)"
  },
  
  "JwtSettings": {
    "SecretKey": "@Microsoft.KeyVault(SecretUri=https://listo-kv.vault.azure.net/secrets/Jwt-SecretKey/)"
  },
  
  "Azure": {
    "KeyVault": {
      "VaultUri": "https://listo-kv.vault.azure.net/",
      "UseManagedIdentity": true
    }
  }
}
```

### Program.cs Integration

```csharp
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUri = builder.Configuration["Azure:KeyVault:VaultUri"];
    if (!string.IsNullOrWhiteSpace(keyVaultUri))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUri),
            new DefaultAzureCredential());
    }
}
```

### Managed Identity vs Service Principal

**Production (Managed Identity - Recommended)**
```json
{
  "Azure": {
    "KeyVault": {
      "VaultUri": "https://listo-kv.vault.azure.net/",
      "UseManagedIdentity": true
    }
  }
}
```

**Development (Service Principal)**
```json
{
  "Azure": {
    "KeyVault": {
      "VaultUri": "https://listo-kv-dev.vault.azure.net/",
      "UseManagedIdentity": false,
      "TenantId": "your-tenant-id",
      "ClientId": "your-client-id",
      "ClientSecret": "@Microsoft.KeyVault(...)"
    }
  }
}
```

### Key Vault Secrets Naming Convention

```
NotificationDb-ConnectionString
Redis-ConnectionString
Jwt-SecretKey
Twilio-AccountSid
Twilio-AuthToken
SendGrid-ApiKey
FCM-ProjectId
ServiceBus-ConnectionString
SignalR-ConnectionString
BlobStorage-ConnectionString
ApplicationInsights-ConnectionString
```

---

## 4. Environment-Specific Configuration

### Development Environment

**Purpose**: Local development with minimal dependencies

**appsettings.Development.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Listo.Notification": "Debug"
    }
  },
  
  "Notification": {
    "RetentionDays": 7,
    "MaxBatchSize": 10
  },
  
  "RateLimiting": {
    "Enabled": false
  },
  
  "CostManagement": {
    "Enabled": false
  },
  
  "FeatureFlags": {
    "EnableServiceBus": false,
    "EnableDetailedErrors": true,
    "EnableApplicationInsights": false
  }
}
```

**Key Characteristics**:
- Rate limiting disabled
- Cost tracking disabled
- Detailed errors enabled
- Local database and Redis
- No Azure services required
- Swagger UI enabled

### Staging Environment

**Purpose**: Pre-production testing with Azure services

**appsettings.Staging.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Listo.Notification": "Debug"
    }
  },
  
  "Notification": {
    "RetentionDays": 30
  },
  
  "RateLimiting": {
    "SmsPerMinute": 10,
    "EmailPerMinute": 20
  },
  
  "Azure": {
    "SignalR": {
      "UseAzureSignalR": true
    }
  },
  
  "FeatureFlags": {
    "EnableServiceBus": true,
    "EnableSwagger": true
  }
}
```

**Key Characteristics**:
- All features enabled
- Reduced rate limits for testing
- Azure services enabled
- Application Insights enabled
- Swagger still available

### Production Environment

**Purpose**: Live production deployment

**appsettings.Production.json**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Listo.Notification": "Information"
    }
  },
  
  "Azure": {
    "SignalR": {
      "UseAzureSignalR": true
    },
    "BlobStorage": {
      "EnableCdn": true
    },
    "ApplicationInsights": {
      "EnableAdaptiveSampling": true,
      "SamplingPercentage": 50.0
    }
  },
  
  "FeatureFlags": {
    "EnableDetailedErrors": false,
    "EnableSwagger": false,
    "EnableApplicationInsights": true
  }
}
```

**Key Characteristics**:
- All secrets from Key Vault
- Minimal logging (Warning level)
- No detailed errors
- Swagger disabled
- All Azure services enabled
- CDN enabled for attachments
- Adaptive sampling for telemetry

---

## 5. Feature Flags

### FeatureFlags Class

```csharp
public class FeatureFlags
{
    // Notification Channels
    public bool EnableSms { get; set; } = true;
    public bool EnableEmail { get; set; } = true;
    public bool EnablePush { get; set; } = true;
    public bool EnableInApp { get; set; } = true;
    
    // Features
    public bool EnableTemplates { get; set; } = true;
    public bool EnableBatchOperations { get; set; } = true;
    public bool EnableScheduledNotifications { get; set; } = true;
    public bool EnableUserPreferences { get; set; } = true;
    public bool EnableAttachments { get; set; } = true;
    
    // Infrastructure
    public bool EnableServiceBus { get; set; } = true;
    public bool EnableSignalR { get; set; } = true;
    public bool EnableWebhooks { get; set; } = true;
    public bool EnableRetry { get; set; } = true;
    public bool EnableProviderFailover { get; set; } = true;
    
    // Operational
    public bool EnableCostManagement { get; set; } = true;
    public bool EnableRateLimiting { get; set; } = true;
    public bool EnableAuditLog { get; set; } = true;
    
    // Development
    public bool EnableDetailedErrors { get; set; } = false;
    public bool EnableSwagger { get; set; } = true;
    public bool EnableApplicationInsights { get; set; } = true;
    public bool EnableHealthChecks { get; set; } = true;
}
```

### Usage in Code

```csharp
// In service or controller
public class NotificationService
{
    private readonly IOptions<FeatureFlags> _featureFlags;
    
    public async Task SendNotificationAsync(...)
    {
        if (!_featureFlags.Value.EnableSms && channel == NotificationChannel.SMS)
        {
            throw new FeatureDisabledException("SMS notifications are currently disabled");
        }
        
        // ... rest of implementation
    }
}
```

### Runtime Toggle

Feature flags can be changed at runtime by updating the configuration source (e.g., Azure App Configuration):

```bash
# Enable a feature in production
az appconfig kv set \
  --name listo-appconfig \
  --key "FeatureFlags:EnableServiceBus" \
  --value true \
  --yes
```

---

## 6. Startup Validation

### ConfigurationValidator

The `ConfigurationValidator` class validates all critical configuration at application startup:

```csharp
public class ConfigurationValidator
{
    public bool ValidateAll(bool isProduction = false)
    {
        // Validate connection strings
        ValidateConnectionStrings();
        
        // Validate JWT settings
        ValidateJwtSettings(isProduction);
        
        // Validate notification providers
        ValidateNotificationProviders(isProduction);
        
        // Validate Azure services
        ValidateAzureServices(isProduction);
        
        // Validate Redis
        ValidateRedis();
        
        // Validate feature flags consistency
        ValidateFeatureFlags();
        
        return !_errors.Any();
    }
}
```

### Integration in Program.cs

```csharp
// Register validation
builder.Services.AddSingleton<ConfigurationValidator>();

// Validate configuration options with data annotations
builder.Services.AddOptionsWithValidation<NotificationOptions>(
    builder.Configuration, 
    NotificationOptions.SectionName);

builder.Services.AddOptionsWithValidation<RedisOptions>(
    builder.Configuration,
    RedisOptions.SectionName);

// ... register other options

var app = builder.Build();

// Run validation at startup
using (var scope = app.Services.CreateScope())
{
    var validator = scope.ServiceProvider.GetRequiredService<ConfigurationValidator>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    if (!validator.ValidateAll(app.Environment.IsProduction()))
    {
        logger.LogCritical("Configuration validation failed");
        foreach (var error in validator.GetErrors())
        {
            logger.LogError("Configuration Error: {Error}", error);
        }
        throw new InvalidOperationException("Invalid configuration detected");
    }
}
```

### Validation Rules

#### Development Environment
- JWT secret can be simple (for testing)
- Provider credentials can be placeholders
- Azure services optional

#### Production Environment
- JWT secret must be >= 32 characters
- No placeholder text in secrets
- All enabled features must have required configuration
- Azure Key Vault must be configured
- All connection strings must be present

---

## 7. Configuration Loading Order

ASP.NET Core loads configuration in the following order (later sources override earlier):

1. **appsettings.json** - Base configuration with defaults
2. **appsettings.{Environment}.json** - Environment-specific overrides
3. **Azure Key Vault** - Secrets (if configured)
4. **Environment Variables** - Highest priority, overrides everything
5. **Command Line Arguments** - For one-off overrides

### Example

```bash
# Base value in appsettings.json
"Notification:MaxBatchSize": 100

# Override in appsettings.Production.json
"Notification:MaxBatchSize": 500

# Override via environment variable
export Notification__MaxBatchSize=1000

# Final value: 1000 (from environment variable)
```

---

## 8. Best Practices

### ✅ DO

1. **Use Strongly-Typed Options**
   ```csharp
   public class MyService
   {
       private readonly NotificationOptions _options;
       
       public MyService(IOptions<NotificationOptions> options)
       {
           _options = options.Value;
       }
   }
   ```

2. **Validate Configuration at Startup**
   ```csharp
   builder.Services.AddOptionsWithValidation<NotificationOptions>(
       builder.Configuration, NotificationOptions.SectionName);
   ```

3. **Use Azure Key Vault in Production**
   ```json
   "ConnectionString": "@Microsoft.KeyVault(SecretUri=...)"
   ```

4. **Provide Sensible Defaults**
   ```csharp
   public int MaxBatchSize { get; set; } = 100;
   ```

5. **Document All Settings**
   ```csharp
   /// <summary>
   /// Maximum batch size for batch operations (1-1000).
   /// </summary>
   [Range(1, 1000)]
   public int MaxBatchSize { get; set; } = 100;
   ```

### ❌ DON'T

1. **Don't Store Secrets in appsettings.json**
   ```json
   ❌ "ApiKey": "sk_live_abc123"
   ✅ "ApiKey": "@Microsoft.KeyVault(...)"
   ```

2. **Don't Use Magic Strings**
   ```csharp
   ❌ var value = config["Notification:MaxBatchSize"];
   ✅ var value = notificationOptions.Value.MaxBatchSize;
   ```

3. **Don't Skip Validation**
   ```csharp
   ❌ var apiKey = config["ApiKey"]; // might be null
   ✅ [Required] public string ApiKey { get; set; }
   ```

4. **Don't Hardcode Environments**
   ```csharp
   ❌ if (env == "Production")
   ✅ if (!builder.Environment.IsDevelopment())
   ```

---

## Configuration Files Summary

### Created Files

| File | Purpose |
|------|---------|
| `NotificationOptions.cs` | General service configuration |
| `AzureOptions.cs` | Azure service configurations (ServiceBus, SignalR, BlobStorage, KeyVault, AppInsights) |
| `RedisOptions.cs` | Redis, rate limiting, cost management |
| `FeatureFlags.cs` | Runtime feature toggles |
| `ConfigurationValidator.cs` | Startup validation and fail-fast |
| `appsettings.json` | Base configuration with defaults |
| `appsettings.Development.json` | Local development settings |
| `appsettings.Staging.json` | Pre-production settings |
| `appsettings.Production.json` | Production settings with Key Vault references |

---

## Next Steps

1. **Configure Azure Key Vault**
   - Create Key Vault instance
   - Add secrets with proper naming convention
   - Configure managed identity or service principal access

2. **Set Up Environment Variables**
   - Development: Use `dotnet user-secrets`
   - Staging/Production: Configure in Azure App Service

3. **Test Configuration Validation**
   - Run application in each environment
   - Verify validation catches missing required settings
   - Test with invalid values

4. **Document Secret Rotation**
   - JWT secret: Annually
   - Provider API keys: As required by provider
   - Database passwords: Quarterly
   - Service principal secrets: Every 6 months

---

## Related Documentation

- [Section 2: Technology Stack](../NOTIFICATION_MGMT_PLAN.md#2-technology-stack)
- [Section 5: Authentication & Authorization](./AUTHENTICATION_CONFIGURATION.md)
- [Section 7: Cost Management & Rate Limiting](./COST_MANAGEMENT_RATE_LIMITING.md)
- [Azure Key Vault Documentation](https://learn.microsoft.com/en-us/azure/key-vault/)
- [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-20  
**Status**: ✅ Complete
