using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Listo.Notification.Infrastructure.Data;
using Listo.Notification.Application.Interfaces;
using Listo.Notification.Infrastructure.Providers;
using Microsoft.Extensions.Options;
using Listo.Notification.API.Hubs;
using Listo.Notification.API.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Listo.Notification.Application.Validators;
using Listo.Notification.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure HTTPS and HSTS for production
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
        options.HttpsPort = 443;
    });

    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
}

// Add services to the container
builder.Services.AddControllers();

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<SendNotificationRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Listo Notification API",
        Version = "v1",
        Description = "Notification management service for ListoExpress platform with multi-channel delivery"
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure Entity Framework
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
var key = Encoding.ASCII.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
    
    // Enable JWT authentication for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
    };
});

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // Admin-only endpoints (cost tracking, templates management, rate limit overrides)
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    // Support and Admin access (view audit logs, conversations)
    options.AddPolicy("SupportAccess", policy =>
        policy.RequireRole("Admin", "Support"));

    // Service-to-Service internal endpoints
    options.AddPolicy("ServiceOnly", policy =>
        policy.RequireRole("Service"));

    // Manage notification templates (create, update, delete)
    options.AddPolicy("ManageTemplates", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("permissions", "notifications.templates.write"));

    // Manage rate limits and budgets
    options.AddPolicy("ManageBudgets", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("permissions", "notifications.budgets.write"));
});

// Register repositories
builder.Services.AddScoped<INotificationRepository, Listo.Notification.Infrastructure.Repositories.NotificationRepository>();

// Register application services
builder.Services.AddScoped<INotificationService, Listo.Notification.Application.Services.NotificationService>();
builder.Services.AddScoped<ITemplateRenderingService, Listo.Notification.Application.Services.TemplateRenderingService>();
builder.Services.AddScoped<IRateLimiterService, Listo.Notification.Infrastructure.Services.RedisTokenBucketLimiter>();
builder.Services.AddScoped<Listo.Notification.Application.Services.RateLimitingService>();
builder.Services.AddScoped<Listo.Notification.Application.Services.BudgetEnforcementService>();
builder.Services.AddSingleton<Listo.Notification.Infrastructure.Services.RedisTokenBucketLimiter>();

// Register Azure Blob Storage for attachments
builder.Services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();

// Register notification providers
builder.Services.AddSingleton<ISmsProvider, TwilioSmsProvider>();
builder.Services.AddSingleton<IEmailProvider, SendGridEmailProvider>();
builder.Services.Configure<TwilioOptions>(builder.Configuration.GetSection("Twilio"));
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
builder.Services.AddSingleton<ISmsProvider, TwilioSmsProvider>();
builder.Services.AddSingleton<IEmailProvider, SendGridEmailProvider>();

// Register Section 9: Real-Time Messaging services
builder.Services.AddScoped<IPresenceTrackingService, Listo.Notification.Infrastructure.Services.PresenceTrackingService>();
builder.Services.AddScoped<IReadReceiptService, Listo.Notification.Infrastructure.Services.ReadReceiptService>();
builder.Services.AddScoped<ITypingIndicatorService, Listo.Notification.Infrastructure.Services.TypingIndicatorService>();

// Configure SignalR
// Environment-based configuration:
// - Development: In-memory (for local testing)
// - Production with Azure SignalR Service: AddAzureSignalR(connectionString)
// - Production self-hosted: AddStackExchangeRedis(redisConnectionString)
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 102400; // 100KB max message size
    options.StreamBufferCapacity = 10;
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(1);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
});

// Add rate limiting filter for SignalR hubs
builder.Services.AddSingleton<Listo.Notification.API.Filters.SignalRRateLimitFilter>();

if (builder.Environment.IsProduction())
{
    // Production: Self-hosted with Redis backplane
    // Note: To use Azure SignalR Service, install Microsoft.Azure.SignalR NuGet package
    // and call signalRBuilder.AddAzureSignalR(connectionString)
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Listo.Notification.SignalR");
    });
}
else
{
    // Development: In-memory (no external dependencies)
    // SignalR works with default configuration
}

// Configure Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit for all endpoints
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Get client IP for rate limiting
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100, // 100 requests
                Window = TimeSpan.FromMinutes(15), // per 15 minutes
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0 // No queueing
            });
    });

    // SMS notification rate limit
    options.AddPolicy("sms", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"sms_{clientIp}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10, // 10 SMS
                Window = TimeSpan.FromMinutes(1), // per minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Email notification rate limit
    options.AddPolicy("email", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"email_{clientIp}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30, // 30 emails
                Window = TimeSpan.FromMinutes(1), // per minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Push notification rate limit
    options.AddPolicy("push", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"push_{clientIp}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60, // 60 push notifications
                Window = TimeSpan.FromMinutes(1), // per minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // In-app notification rate limit
    options.AddPolicy("inapp", context =>
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"inapp_{clientIp}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120, // 120 in-app messages
                Window = TimeSpan.FromMinutes(1), // per minute
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            await context.HttpContext.Response.WriteAsync(
                $"Too many requests. Please retry after {retryAfter.TotalSeconds} seconds.",
                cancellationToken);
        }
        else
        {
            await context.HttpContext.Response.WriteAsync(
                "Too many requests. Please retry later.",
                cancellationToken);
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Enable HSTS in production
    app.UseHsts();
}

app.UseHttpsRedirection();

// Request validation (before authentication)
app.UseRequestValidation();

// Enable rate limiting
app.UseRateLimiter();

// Service-to-service authentication (for internal endpoints)
app.UseServiceSecretAuthentication();

// Standard authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Set tenant context from JWT claims
app.UseTenantContext();

app.MapControllers();

// Map SignalR hubs
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<MessagingHub>("/hubs/messaging");

app.Run();
