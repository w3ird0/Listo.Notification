using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Listo.Notification.Infrastructure.Configuration;

/// <summary>
/// Validates configuration at startup to fail fast if critical settings are missing or invalid.
/// </summary>
public class ConfigurationValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationValidator> _logger;
    private readonly List<string> _errors = new();

    public ConfigurationValidator(
        IConfiguration configuration,
        ILogger<ConfigurationValidator> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates all critical configuration sections.
    /// </summary>
    /// <param name="isProduction">Whether running in production environment</param>
    /// <returns>True if all validations pass, false otherwise</returns>
    public bool ValidateAll(bool isProduction = false)
    {
        _errors.Clear();

        _logger.LogInformation("Starting configuration validation...");

        // Validate connection strings
        ValidateConnectionStrings();

        // Validate JWT settings
        ValidateJwtSettings(isProduction);

        // Validate notification providers
        ValidateNotificationProviders(isProduction);

        // Validate Azure services (if enabled)
        ValidateAzureServices(isProduction);

        // Validate Redis
        ValidateRedis();

        // Validate feature flags consistency
        ValidateFeatureFlags();

        if (_errors.Any())
        {
            _logger.LogError("Configuration validation failed with {ErrorCount} error(s):", _errors.Count);
            foreach (var error in _errors)
            {
                _logger.LogError("  - {Error}", error);
            }
            return false;
        }

        _logger.LogInformation("Configuration validation completed successfully");
        return true;
    }

    /// <summary>
    /// Gets all validation errors.
    /// </summary>
    public IReadOnlyList<string> GetErrors() => _errors.AsReadOnly();

    private void ValidateConnectionStrings()
    {
        var defaultConnection = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(defaultConnection))
        {
            _errors.Add("ConnectionStrings:DefaultConnection is missing or empty");
        }

        var redisConnection = _configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            _errors.Add("ConnectionStrings:Redis is missing or empty");
        }
    }

    private void ValidateJwtSettings(bool isProduction)
    {
        var secretKey = _configuration["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _errors.Add("JwtSettings:SecretKey is missing or empty");
        }
        else if (isProduction && (secretKey.Contains("REPLACE") || secretKey.Contains("DEVELOPMENT") || secretKey.Length < 32))
        {
            _errors.Add("JwtSettings:SecretKey must be at least 32 characters and not contain placeholder text in production");
        }

        var issuer = _configuration["JwtSettings:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            _errors.Add("JwtSettings:Issuer is missing or empty");
        }

        var audience = _configuration["JwtSettings:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            _errors.Add("JwtSettings:Audience is missing or empty");
        }
    }

    private void ValidateNotificationProviders(bool isProduction)
    {
        var featureFlags = _configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>() ?? new FeatureFlags();

        // Validate Twilio (if SMS is enabled)
        if (featureFlags.EnableSms)
        {
            var twilioAccountSid = _configuration["Twilio:AccountSid"];
            var twilioAuthToken = _configuration["Twilio:AuthToken"];

            if (string.IsNullOrWhiteSpace(twilioAccountSid) || twilioAccountSid.Contains("{{"))
            {
                if (isProduction)
                {
                    _errors.Add("Twilio:AccountSid is missing or contains placeholder - SMS notifications are enabled");
                }
                else
                {
                    _logger.LogWarning("Twilio:AccountSid is not configured - SMS notifications will not work");
                }
            }

            if (string.IsNullOrWhiteSpace(twilioAuthToken) || twilioAuthToken.Contains("{{"))
            {
                if (isProduction)
                {
                    _errors.Add("Twilio:AuthToken is missing or contains placeholder - SMS notifications are enabled");
                }
                else
                {
                    _logger.LogWarning("Twilio:AuthToken is not configured - SMS notifications will not work");
                }
            }
        }

        // Validate SendGrid (if Email is enabled)
        if (featureFlags.EnableEmail)
        {
            var sendGridApiKey = _configuration["SendGrid:ApiKey"];

            if (string.IsNullOrWhiteSpace(sendGridApiKey) || sendGridApiKey.Contains("{{"))
            {
                if (isProduction)
                {
                    _errors.Add("SendGrid:ApiKey is missing or contains placeholder - Email notifications are enabled");
                }
                else
                {
                    _logger.LogWarning("SendGrid:ApiKey is not configured - Email notifications will not work");
                }
            }
        }

        // Validate FCM (if Push is enabled)
        if (featureFlags.EnablePush)
        {
            var fcmProjectId = _configuration["FCM:ProjectId"];
            var fcmCredentialsPath = _configuration["FCM:CredentialsPath"];

            if (string.IsNullOrWhiteSpace(fcmProjectId))
            {
                _logger.LogWarning("FCM:ProjectId is not configured - Push notifications will not work");
            }

            if (string.IsNullOrWhiteSpace(fcmCredentialsPath))
            {
                _logger.LogWarning("FCM:CredentialsPath is not configured - Push notifications will not work");
            }
        }
    }

    private void ValidateAzureServices(bool isProduction)
    {
        var featureFlags = _configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>() ?? new FeatureFlags();

        // Validate Service Bus (if enabled)
        if (featureFlags.EnableServiceBus)
        {
            var serviceBusConnectionString = _configuration["Azure:ServiceBus:ConnectionString"];
            if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
            {
                if (isProduction)
                {
                    _errors.Add("Azure:ServiceBus:ConnectionString is missing - Service Bus is enabled");
                }
                else
                {
                    _logger.LogWarning("Azure:ServiceBus:ConnectionString is not configured - async processing will not work");
                }
            }
        }

        // Validate Blob Storage (if attachments are enabled)
        if (featureFlags.EnableAttachments)
        {
            var blobStorageConnectionString = _configuration["Azure:BlobStorage:ConnectionString"];
            if (string.IsNullOrWhiteSpace(blobStorageConnectionString))
            {
                if (isProduction)
                {
                    _errors.Add("Azure:BlobStorage:ConnectionString is missing - Attachments are enabled");
                }
                else
                {
                    _logger.LogWarning("Azure:BlobStorage:ConnectionString is not configured - attachment uploads will not work");
                }
            }
        }

        // Validate Azure SignalR (if enabled)
        var signalROptions = _configuration.GetSection(SignalROptions.SectionName).Get<SignalROptions>();
        if (signalROptions?.UseAzureSignalR == true)
        {
            var signalRConnectionString = _configuration["Azure:SignalR:ConnectionString"];
            if (string.IsNullOrWhiteSpace(signalRConnectionString))
            {
                _errors.Add("Azure:SignalR:ConnectionString is missing - Azure SignalR is enabled");
            }
        }

        // Validate Key Vault (if configured)
        var keyVaultUri = _configuration["Azure:KeyVault:VaultUri"];
        if (!string.IsNullOrWhiteSpace(keyVaultUri) && isProduction)
        {
            var keyVaultOptions = _configuration.GetSection(KeyVaultOptions.SectionName).Get<KeyVaultOptions>();
            if (keyVaultOptions?.UseManagedIdentity == false)
            {
                if (string.IsNullOrWhiteSpace(keyVaultOptions.ClientId))
                {
                    _errors.Add("Azure:KeyVault:ClientId is required when not using managed identity");
                }
                if (string.IsNullOrWhiteSpace(keyVaultOptions.ClientSecret))
                {
                    _errors.Add("Azure:KeyVault:ClientSecret is required when not using managed identity");
                }
            }
        }
    }

    private void ValidateRedis()
    {
        var redisConnectionString = _configuration["Redis:ConnectionString"] ?? _configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            _errors.Add("Redis:ConnectionString is missing - Redis is required for rate limiting and caching");
        }
    }

    private void ValidateFeatureFlags()
    {
        var featureFlags = _configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>() ?? new FeatureFlags();

        // Warn if SignalR is enabled but real-time features are disabled
        if (featureFlags.EnableSignalR && !featureFlags.EnableInApp && !featureFlags.EnableInAppMessaging)
        {
            _logger.LogWarning("SignalR is enabled but both in-app notifications and in-app messaging are disabled");
        }

        // Warn if cost management is disabled in production
        if (!featureFlags.EnableCostManagement)
        {
            _logger.LogWarning("Cost management is disabled - budget tracking and enforcement will not work");
        }

        // Warn if rate limiting is disabled
        if (!featureFlags.EnableRateLimiting)
        {
            _logger.LogWarning("Rate limiting is disabled - API may be susceptible to abuse");
        }
    }

    /// <summary>
    /// Validates a specific options class using data annotations.
    /// </summary>
    public static bool ValidateOptions<T>(T options, out List<ValidationResult> validationResults) where T : class
    {
        validationResults = new List<ValidationResult>();
        var context = new ValidationContext(options);
        return Validator.TryValidateObject(options, context, validationResults, validateAllProperties: true);
    }
}

/// <summary>
/// Extension methods for IServiceCollection to add configuration validation.
/// </summary>
public static class ConfigurationValidationExtensions
{
    /// <summary>
    /// Adds configuration validation with data annotations.
    /// </summary>
    public static IServiceCollection AddOptionsWithValidation<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName) where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
