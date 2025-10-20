# Sections 9-17 Expansion Content

This file contains the comprehensive expansions for sections 9-17 that will be merged into the main NOTIFICATION_MGMT_PLAN.md

## 9. Notification Sending Integrations

This section details the implementation of notification providers for Push, SMS, and Email channels.

### 9.1. Firebase Cloud Messaging (FCM) Integration

**Installation:**
```bash
dotnet add package FirebaseAdmin
```

**Configuration (appsettings.json):**
```json
{
  "Firebase": {
    "ProjectId": "listo-notification",
    "CredentialsPath": "firebase-credentials.json",
    "ServiceAccountKeyFromKeyVault": "firebase-service-account-key"
  }
}
```

**Implementation:**
```csharp
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Listo.Notification.Infrastructure.Providers.Interfaces;

namespace Listo.Notification.Infrastructure.Providers;

public class FcmPushProvider : IFcmPushProvider
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FcmPushProvider> _logger;
    private readonly IEncryptionService _encryptionService;

    public FcmPushProvider(
        IConfiguration configuration,
        ILogger<FcmPushProvider> logger,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _encryptionService = encryptionService;

        // Initialize Firebase App
        var credentialsJson = configuration["Firebase:ServiceAccountKeyFromKeyVault"];
        var credential = GoogleCredential.FromJson(credentialsJson);
        
        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = credential,
            ProjectId = configuration["Firebase:ProjectId"]
        });

        _messaging = FirebaseMessaging.GetMessaging(app);
    }

    public async Task<FcmSendResult> SendNotificationAsync(
        string encryptedToken,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        try
        {
            // Decrypt the token
            var decryptedToken = await _encryptionService.DecryptAsync(encryptedToken);

            var message = new Message
            {
                Token = decryptedToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ChannelId = "default"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default",
                        Badge = 1
                    }
                }
            };

            var response = await _messaging.SendAsync(message);

            _logger.LogInformation(
                "FCM push notification sent successfully. MessageId: {MessageId}",
                response);

            return new FcmSendResult
            {
                IsSuccess = true,
                MessageId = response
            };
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex,
                "FCM push notification failed. ErrorCode: {ErrorCode}",
                ex.ErrorCode);

            return new FcmSendResult
            {
                IsSuccess = false,
                ErrorCode = ex.ErrorCode?.ToString(),
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending FCM push notification");
            throw;
        }
    }

    public async Task<BatchFcmSendResult> SendBatchNotificationsAsync(
        List<(string encryptedToken, string title, string body, Dictionary<string, string>? data)> notifications)
    {
        var messages = new List<Message>();
        
        foreach (var (encryptedToken, title, body, data) in notifications)
        {
            var decryptedToken = await _encryptionService.DecryptAsync(encryptedToken);
            
            messages.Add(new Message
            {
                Token = decryptedToken,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data
            });
        }

        try
        {
            var response = await _messaging.SendEachAsync(messages);

            _logger.LogInformation(
                "FCM batch notification sent. Success: {SuccessCount}, Failure: {FailureCount}",
                response.SuccessCount,
                response.FailureCount);

            return new BatchFcmSendResult
            {
                SuccessCount = response.SuccessCount,
                FailureCount = response.FailureCount,
                Responses = response.Responses.Select(r => new FcmSendResult
                {
                    IsSuccess = r.IsSuccess,
                    MessageId = r.MessageId,
                    ErrorCode = r.Exception?.ErrorCode?.ToString(),
                    ErrorMessage = r.Exception?.Message
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending FCM batch notification");
            throw;
        }
    }
}
```

---

### 9.2. Twilio SMS Integration

**Installation:**
```bash
dotnet add package Twilio
```

**Configuration (appsettings.json):**
```json
{
  "Twilio": {
    "AccountSid": "{KeyVault:twilio-account-sid}",
    "AuthToken": "{KeyVault:twilio-auth-token}",
    "PhoneNumber": "+15551234567",
    "MessagingServiceSid": "MGxxxx"
  }
}
```

**Implementation:**
```csharp
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Listo.Notification.Infrastructure.Providers.Interfaces;

namespace Listo.Notification.Infrastructure.Providers;

public class TwilioSmsProvider : ITwilioSmsProvider
{
    private readonly ILogger<TwilioSmsProvider> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly string _twilioPhoneNumber;
    private readonly string? _messagingServiceSid;

    public TwilioSmsProvider(
        IConfiguration configuration,
        ILogger<TwilioSmsProvider> logger,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _encryptionService = encryptionService;

        var accountSid = configuration["Twilio:AccountSid"];
        var authToken = configuration["Twilio:AuthToken"];
        _twilioPhoneNumber = configuration["Twilio:PhoneNumber"] 
            ?? throw new ArgumentNullException("Twilio:PhoneNumber is required");
        _messagingServiceSid = configuration["Twilio:MessagingServiceSid"];

        TwilioClient.Init(accountSid, authToken);
    }

    public async Task<SmsSendResult> SendSmsAsync(
        string encryptedPhoneNumber,
        string message)
    {
        try
        {
            // Decrypt the phone number
            var decryptedPhoneNumber = await _encryptionService.DecryptAsync(encryptedPhoneNumber);

            var messageOptions = new CreateMessageOptions(new PhoneNumber(decryptedPhoneNumber))
            {
                Body = message
            };

            // Use Messaging Service SID if configured (better for high volume)
            if (!string.IsNullOrEmpty(_messagingServiceSid))
            {
                messageOptions.MessagingServiceSid = _messagingServiceSid;
            }
            else
            {
                messageOptions.From = new PhoneNumber(_twilioPhoneNumber);
            }

            var messageResource = await MessageResource.CreateAsync(messageOptions);

            _logger.LogInformation(
                "SMS sent successfully. MessageSid: {MessageSid}, Status: {Status}",
                messageResource.Sid,
                messageResource.Status);

            return new SmsSendResult
            {
                IsSuccess = messageResource.ErrorCode == null,
                MessageId = messageResource.Sid,
                Status = messageResource.Status.ToString(),
                SegmentCount = messageResource.NumSegments ?? 1,
                ErrorCode = messageResource.ErrorCode?.ToString(),
                ErrorMessage = messageResource.ErrorMessage
            };
        }
        catch (Twilio.Exceptions.ApiException ex)
        {
            _logger.LogError(ex,
                "Twilio SMS failed. Code: {Code}, Status: {Status}",
                ex.Code,
                ex.Status);

            return new SmsSendResult
            {
                IsSuccess = false,
                ErrorCode = ex.Code.ToString(),
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending SMS");
            throw;
        }
    }

    public async Task<int> GetSegmentCount(string message)
    {
        // GSM-7 encoding: 160 characters per segment
        // Unicode encoding: 70 characters per segment
        var isUnicode = message.Any(c => c > 127);
        var charsPerSegment = isUnicode ? 70 : 160;
        var segmentCount = (int)Math.Ceiling((double)message.Length / charsPerSegment);
        
        return await Task.FromResult(segmentCount);
    }
}
```

---

### 9.3. SendGrid Email Integration

**Installation:**
```bash
dotnet add package SendGrid
```

**Configuration (appsettings.json):**
```json
{
  "SendGrid": {
    "ApiKey": "{KeyVault:sendgrid-api-key}",
    "FromEmail": "noreply@listoexpress.com",
    "FromName": "ListoExpress",
    "TrackOpens": true,
    "TrackClicks": true
  }
}
```

**Implementation:**
```csharp
using SendGrid;
using SendGrid.Helpers.Mail;
using Listo.Notification.Infrastructure.Providers.Interfaces;

namespace Listo.Notification.Infrastructure.Providers;

public class SendGridEmailProvider : IEmailProvider
{
    private readonly SendGridClient _client;
    private readonly ILogger<SendGridEmailProvider> _logger;
    private readonly IEncryptionService _encryptionService;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _trackOpens;
    private readonly bool _trackClicks;

    public SendGridEmailProvider(
        IConfiguration configuration,
        ILogger<SendGridEmailProvider> logger,
        IEncryptionService encryptionService)
    {
        _logger = logger;
        _encryptionService = encryptionService;

        var apiKey = configuration["SendGrid:ApiKey"];
        _fromEmail = configuration["SendGrid:FromEmail"] 
            ?? throw new ArgumentNullException("SendGrid:FromEmail is required");
        _fromName = configuration["SendGrid:FromName"] ?? "ListoExpress";
        _trackOpens = bool.Parse(configuration["SendGrid:TrackOpens"] ?? "true");
        _trackClicks = bool.Parse(configuration["SendGrid:TrackClicks"] ?? "true");

        _client = new SendGridClient(apiKey);
    }

    public async Task<EmailSendResult> SendEmailAsync(
        string encryptedEmail,
        string subject,
        string htmlContent,
        string? plainTextContent = null)
    {
        try
        {
            // Decrypt the email
            var decryptedEmail = await _encryptionService.DecryptAsync(encryptedEmail);

            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(decryptedEmail);
            
            var msg = MailHelper.CreateSingleEmail(
                from,
                to,
                subject,
                plainTextContent ?? StripHtml(htmlContent),
                htmlContent);

            // Tracking settings
            msg.SetOpenTracking(_trackOpens);
            msg.SetClickTracking(_trackClicks, false);

            var response = await _client.SendEmailAsync(msg);

            var isSuccess = response.IsSuccessStatusCode;

            if (isSuccess)
            {
                _logger.LogInformation(
                    "Email sent successfully. StatusCode: {StatusCode}",
                    response.StatusCode);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogError(
                    "SendGrid email failed. StatusCode: {StatusCode}, Body: {Body}",
                    response.StatusCode,
                    body);
            }

            return new EmailSendResult
            {
                IsSuccess = isSuccess,
                MessageId = response.Headers.GetValues("X-Message-Id").FirstOrDefault(),
                StatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email");
            throw;
        }
    }

    public async Task<BatchEmailSendResult> SendBatchEmailAsync(
        List<(string encryptedEmail, string subject, string htmlContent, string? plainTextContent)> emails)
    {
        var tasks = emails.Select(e => 
            SendEmailAsync(e.encryptedEmail, e.subject, e.htmlContent, e.plainTextContent));
        
        var results = await Task.WhenAll(tasks);

        return new BatchEmailSendResult
        {
            SuccessCount = results.Count(r => r.IsSuccess),
            FailureCount = results.Count(r => !r.IsSuccess),
            Results = results.ToList()
        };
    }

    private string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }
}
```

---

### 9.4. Provider Result Models

```csharp
namespace Listo.Notification.Infrastructure.Providers.Models;

public class FcmSendResult
{
    public bool IsSuccess { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BatchFcmSendResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<FcmSendResult> Responses { get; set; } = new();
}

public class SmsSendResult
{
    public bool IsSuccess { get; set; }
    public string? MessageId { get; set; }
    public string? Status { get; set; }
    public int SegmentCount { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EmailSendResult
{
    public bool IsSuccess { get; set; }
    public string? MessageId { get; set; }
    public int StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BatchEmailSendResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<EmailSendResult> Results { get; set; } = new();
}
```

---

### 9.5. Retry Logic with Polly

**Installation:**
```bash
dotnet add package Polly
dotnet add package Polly.Extensions.Http
```

**Configuration:**
```csharp
using Polly;
using Polly.Extensions.Http;

namespace Listo.Notification.Infrastructure.Extensions;

public static class PollyPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode >= 500)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + 
                    TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempt
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
```

---

## 10. File & Image Upload Handling

This section covers file upload to Azure Blob Storage for in-app messaging attachments.

### 10.1. Azure Blob Storage Configuration

**Installation:**
```bash
dotnet add package Azure.Storage.Blobs
```

**Configuration (appsettings.json):**
```json
{
  "BlobStorage": {
    "ConnectionString": "{KeyVault:blob-storage-connection-string}",
    "ContainerName": "message-attachments",
    "MaxFileSizeMB": 10,
    "AllowedFileTypes": [".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx"],
    "CdnUrl": "https://cdn.listoexpress.com"
  }
}
```

---

### 10.2. Blob Storage Service Implementation

```csharp
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Listo.Notification.Infrastructure.Storage.Interfaces;

namespace Listo.Notification.Infrastructure.Storage;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly int _maxFileSizeMB;
    private readonly string[] _allowedFileTypes;
    private readonly string _cdnUrl;

    public AzureBlobStorageService(
        IConfiguration configuration,
        ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;

        var connectionString = configuration["BlobStorage:ConnectionString"];
        var containerName = configuration["BlobStorage:ContainerName"];
        _maxFileSizeMB = int.Parse(configuration["BlobStorage:MaxFileSizeMB"] ?? "10");
        _allowedFileTypes = configuration.GetSection("BlobStorage:AllowedFileTypes").Get<string[]>() 
            ?? new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        _cdnUrl = configuration["BlobStorage:CdnUrl"] ?? string.Empty;

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        
        // Ensure container exists
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<Result<FileUploadResult>> UploadFileAsync(
        IFormFile file,
        string folder,
        string userId)
    {
        try
        {
            // Validate file
            var validationResult = ValidateFile(file);
            if (!validationResult.IsSuccess)
                return Result<FileUploadResult>.Failure(
                    validationResult.Error, 
                    validationResult.Message);

            // Generate unique filename
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid()}{extension}";
            var blobPath = $"{folder}/{userId}/{fileName}";

            var blobClient = _containerClient.GetBlobClient(blobPath);

            // Upload with metadata
            var metadata = new Dictionary<string, string>
            {
                ["OriginalFileName"] = file.FileName,
                ["UploadedBy"] = userId,
                ["UploadedAt"] = DateTime.UtcNow.ToString("o"),
                ["ContentType"] = file.ContentType
            };

            await using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = file.ContentType
            });

            await blobClient.SetMetadataAsync(metadata);

            var fileUrl = string.IsNullOrEmpty(_cdnUrl)
                ? blobClient.Uri.ToString()
                : $"{_cdnUrl}/{blobPath}";

            _logger.LogInformation(
                "File uploaded successfully. Path: {BlobPath}, User: {UserId}",
                blobPath,
                userId);

            return Result<FileUploadResult>.Success(new FileUploadResult
            {
                FileName = fileName,
                OriginalFileName = file.FileName,
                Url = fileUrl,
                BlobPath = blobPath,
                SizeBytes = file.Length,
                ContentType = file.ContentType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to blob storage");
            return Result<FileUploadResult>.Failure(
                "UPLOAD_ERROR",
                "Failed to upload file");
        }
    }

    public async Task<Result> DeleteFileAsync(string blobPath)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobPath);
            await blobClient.DeleteIfExistsAsync();

            _logger.LogInformation("File deleted successfully. Path: {BlobPath}", blobPath);

            return Result.Success("File deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from blob storage");
            return Result.Failure("DELETE_ERROR", "Failed to delete file");
        }
    }

    public async Task<Result<string>> GetSasUrlAsync(string blobPath, TimeSpan validity)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobPath);
            
            var sasUri = blobClient.GenerateSasUri(
                Azure.Storage.Sas.BlobSasPermissions.Read,
                DateTimeOffset.UtcNow.Add(validity));

            return Result<string>.Success(sasUri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAS URL");
            return Result<string>.Failure("SAS_ERROR", "Failed to generate SAS URL");
        }
    }

    private Result ValidateFile(IFormFile file)
    {
        // Check if file exists
        if (file == null || file.Length == 0)
            return Result.Failure("INVALID_FILE", "File is empty or null");

        // Check file size
        var maxSizeBytes = _maxFileSizeMB * 1024 * 1024;
        if (file.Length > maxSizeBytes)
            return Result.Failure(
                "FILE_TOO_LARGE",
                $"File size exceeds maximum allowed size of {_maxFileSizeMB}MB");

        // Check file type
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedFileTypes.Contains(extension))
            return Result.Failure(
                "INVALID_FILE_TYPE",
                $"File type {extension} is not allowed. Allowed types: {string.Join(", ", _allowedFileTypes)}");

        // Additional security: Check content type
        var isValidContentType = file.ContentType.StartsWith("image/") ||
                                 file.ContentType == "application/pdf" ||
                                 file.ContentType.StartsWith("application/vnd.") ||
                                 file.ContentType.StartsWith("application/msword");

        if (!isValidContentType)
            return Result.Failure(
                "INVALID_CONTENT_TYPE",
                "File content type is not allowed");

        return Result.Success();
    }
}

public class FileUploadResult
{
    public string FileName { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string Url { get; set; } = null!;
    public string BlobPath { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string ContentType { get; set; } = null!;
}
```

---

### 10.3. File Upload Security Middleware

```csharp
using Microsoft.AspNetCore.Http.Features;

namespace Listo.Notification.API.Middleware;

public class FileUploadSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FileUploadSecurityMiddleware> _logger;
    private readonly long _maxRequestBodySize = 10 * 1024 * 1024; // 10 MB

    public FileUploadSecurityMiddleware(
        RequestDelegate next,
        ILogger<FileUploadSecurityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to file upload endpoints
        if (context.Request.Path.StartsWithSegments("/api/v1/conversations") &&
            context.Request.Method == "POST" &&
            context.Request.ContentType?.Contains("multipart/form-data") == true)
        {
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature != null)
            {
                feature.MaxRequestBodySize = _maxRequestBodySize;
            }

            // Validate content length
            if (context.Request.ContentLength > _maxRequestBodySize)
            {
                context.Response.StatusCode = 413; // Payload Too Large
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "PAYLOAD_TOO_LARGE",
                    message = $"Request body size exceeds maximum allowed size of {_maxRequestBodySize / 1024 / 1024}MB"
                });
                return;
            }
        }

        await _next(context);
    }
}
```

---

## 11. Testing Strategy

This section covers unit testing, integration testing, and test automation.

### 11.1. Unit Testing Setup

**Installation:**
```bash
dotnet add package xUnit
dotnet add package xUnit.runner.visualstudio
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

### 11.2. Unit Test Examples

#### NotificationService Tests

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using Listo.Notification.Application.Services;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Listo.Notification.Tests.Unit.Services;

public class NotificationServiceTests
{
    private readonly Mock<IFcmPushProvider> _fcmProviderMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly NotificationDbContext _context;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _fcmProviderMock = new Mock<IFcmPushProvider>();
        _loggerMock = new Mock<ILogger<NotificationService>>();

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new NotificationDbContext(options);

        _sut = new NotificationService(
            _context,
            _fcmProviderMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_WithValidUserId_ReturnsNotifications()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _context.Notifications.AddRange(
            new Domain.Entities.Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId.ToString(),
                Title = "Test 1",
                Body = "Body 1",
                Channel = "push",
                CreatedAt = DateTime.UtcNow
            },
            new Domain.Entities.Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId.ToString(),
                Title = "Test 2",
                Body = "Body 2",
                Channel = "email",
                CreatedAt = DateTime.UtcNow
            }
        );
        await _context.SaveChangesAsync();

        var parameters = new NotificationQueryParameters
        {
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await _sut.GetUserNotificationsAsync(userId, parameters);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task SendPushNotificationAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var encryptedToken = "encrypted_token";
        var title = "Test Notification";
        var body = "Test Body";

        _fcmProviderMock
            .Setup(x => x.SendNotificationAsync(encryptedToken, title, body, It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync(new FcmSendResult { IsSuccess = true, MessageId = "msg-123" });

        // Act
        var result = await _sut.SendPushNotificationAsync(encryptedToken, title, body);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.MessageId.Should().Be("msg-123");
        _fcmProviderMock.Verify(
            x => x.SendNotificationAsync(encryptedToken, title, body, It.IsAny<Dictionary<string, string>>()), 
            Times.Once);
    }
}
```

---

### 11.3. Integration Testing Setup

**Installation:**
```bash
dotnet add package Testcontainers
dotnet add package Testcontainers.MsSql
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

#### Integration Test Base Class

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace Listo.Notification.Tests.Integration;

public class IntegrationTestBase : IAsyncLifetime
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    private readonly MsSqlContainer _dbContainer;

    public IntegrationTestBase()
    {
        _dbContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace SQL Server connection with test container
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<NotificationDbContext>));
                    
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<NotificationDbContext>(options =>
                    {
                        options.UseSqlServer(_dbContainer.GetConnectionString());
                    });
                });
            });

        Client = Factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        
        // Run migrations
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await Factory.DisposeAsync();
        Client.Dispose();
    }
}
```

#### Integration Test Example

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Listo.Notification.Tests.Integration;

public class NotificationsControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetNotifications_WithValidToken_ReturnsNotifications()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        Client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await Client.GetAsync("/api/v1/notifications");

        // Assert
        response.Should().BeSuccessful();
        var notifications = await response.Content.ReadFromJsonAsync<PaginatedResponse<NotificationDto>>();
        notifications.Should().NotBeNull();
    }

    [Fact]
    public async Task SendNotification_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/notifications", new
        {
            userId = Guid.NewGuid(),
            channel = "push",
            templateKey = "test_notification",
            priority = "normal",
            data = new Dictionary<string, string> { ["key"] = "value" }
        });

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private async Task<string> GetAuthTokenAsync()
    {
        // Implementation to get test auth token
        return "test_token";
    }
}
```

---

### 11.4. Test Coverage Configuration

**coverlet.runsettings:**
```xml
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>opencover,cobertura</Format>
          <Exclude>[*]*.Migrations.*,[*]*Program,[*]*Startup</Exclude>
          <Include>[Listo.Notification.*]*</Include>
          <ExcludeByAttribute>Obsolete,GeneratedCode,CompilerGenerated</ExcludeByAttribute>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

**Run tests with coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

---

## 12. Containerization

This section covers Docker containerization and deployment configuration.

### 12.1. Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["Listo.Notification.API/Listo.Notification.API.csproj", "Listo.Notification.API/"]
COPY ["Listo.Notification.Application/Listo.Notification.Application.csproj", "Listo.Notification.Application/"]
COPY ["Listo.Notification.Domain/Listo.Notification.Domain.csproj", "Listo.Notification.Domain/"]
COPY ["Listo.Notification.Infrastructure/Listo.Notification.Infrastructure.csproj", "Listo.Notification.Infrastructure/"]

RUN dotnet restore "Listo.Notification.API/Listo.Notification.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/Listo.Notification.API"
RUN dotnet build "Listo.Notification.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Listo.Notification.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "Listo.Notification.API.dll"]
```

---

### 12.2. .dockerignore

```
**/.dockerignore
**/.env
**/.git
**/.gitignore
**/.vs
**/.vscode
**/*.*proj.user
**/azds.yaml
**/bin
**/charts
**/docker-compose*
**/compose*
**/Dockerfile*
**/node_modules
**/npm-debug.log
**/obj
**/secrets.dev.yaml
**/values.dev.yaml
**/build
**/dist
**/.DS_Store
**/coverage
**/TestResults
README.md
```

---

### 12.3. docker-compose.yml (for local development)

```yaml
version: '3.8'

services:
  notification-api:
    build:
      context: .
      dockerfile: Listo.Notification.API/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=ListoNotification;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True
      - Auth__Authority=http://localhost:5001
      - Auth__Audience=listo-notification-api
    depends_on:
      - sqlserver
      - redis
    networks:
      - listo-network

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Passw0rd
      - MSSQL_PID=Express
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    networks:
      - listo-network

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redisdata:/data
    networks:
      - listo-network

volumes:
  sqldata:
  redisdata:

networks:
  listo-network:
    driver: bridge
```

---

### 12.4. Environment Configuration

**.env.example:**
```bash
# Database
CONNECTION_STRING=Server=localhost,1433;Database=ListoNotification;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True

# Auth
AUTH__AUTHORITY=https://auth.listoexpress.com
AUTH__AUDIENCE=listo-notification-api

# Azure Key Vault
KEYVAULT__VAULT_URI=https://listo-keyvault.vault.azure.net/

# Firebase
FIREBASE__PROJECT_ID=listo-notification

# Twilio
TWILIO__ACCOUNT_SID={from_key_vault}
TWILIO__AUTH_TOKEN={from_key_vault}
TWILIO__PHONE_NUMBER=+15551234567

# SendGrid
SENDGRID__API_KEY={from_key_vault}
SENDGRID__FROM_EMAIL=noreply@listoexpress.com

# Azure Blob Storage
BLOBSTORAGE__CONNECTION_STRING={from_key_vault}
BLOBSTORAGE__CONTAINER_NAME=message-attachments

# Redis
REDIS__CONNECTION_STRING=localhost:6379

# CORS
CORS__ALLOWED_ORIGINS=https://app.listoexpress.com,https://admin.listoexpress.com

# Serilog
SERILOG__MINIMUMLEVEL=Information
```

---

This completes sections 9-12. I'll continue with the remaining sections 13-17 in the next part.
