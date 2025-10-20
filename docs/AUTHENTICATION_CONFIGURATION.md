# Authentication & Authorization Configuration Guide

**Service:** Listo.Notification  
**Last Updated:** 2025-01-20

---

## Overview

The Listo.Notification service implements dual authentication mechanisms:
1. **JWT Bearer Tokens** - For client applications (mobile apps, web apps)
2. **Service Secrets** - For service-to-service communication

---

## 1. JWT Bearer Token Authentication

### Configuration (appsettings.json)

```json
{
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",  // Store in Azure Key Vault
    "Issuer": "https://auth.listoexpress.com",
    "Audience": "listo-notification-api",
    "ExpirationMinutes": 60,
    "ClockSkew": 0
  }
}
```

### Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `JWT_SECRET_KEY` | Symmetric key for JWT validation | `your-256-bit-secret` |

### Required Claims

| Claim | Type | Description |
|-------|------|-------------|
| `sub` | string | User ID (subject) |
| `email` | string | User email address |
| `role` | string | User role (Customer, Driver, Support, Admin) |
| `tenant_id` | GUID | Tenant identifier for multi-tenancy |
| `permissions` | array | Granular permissions array |

### Usage in Controllers

```csharp
[Authorize] // Requires any authenticated user
public class NotificationsController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetNotification(Guid id)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = User.FindFirst("tenant_id")?.Value;
        // ...
    }

    [Authorize(Roles = "Admin")] // Requires Admin role
    [HttpPost("admin/broadcast")]
    public async Task<IActionResult> BroadcastNotification() { }
}
```

### SignalR WebSocket Authentication

JWT tokens are passed via query string for WebSocket connections:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
        accessTokenFactory: () => jwtToken
    })
    .build();
```

**Server Configuration:**

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
```

---

## 2. Service-to-Service Authentication

### Configuration (appsettings.json)

```json
{
  "ServiceSecrets": {
    "Auth": "${AUTH_SERVICE_SECRET}",
    "Orders": "${ORDERS_SERVICE_SECRET}",
    "RideSharing": "${RIDESHARING_SERVICE_SECRET}",
    "Products": "${PRODUCTS_SERVICE_SECRET}"
  }
}
```

### Environment Variables (Azure Key Vault)

| Variable | Description |
|----------|-------------|
| `AUTH_SERVICE_SECRET` | Shared secret for Listo.Auth service |
| `ORDERS_SERVICE_SECRET` | Shared secret for Listo.Orders service |
| `RIDESHARING_SERVICE_SECRET` | Shared secret for Listo.RideSharing service |
| `PRODUCTS_SERVICE_SECRET` | Shared secret for Listo.Products service |

### Required Headers

| Header | Description | Example |
|--------|-------------|---------|
| `X-Service-Secret` | Shared secret for authentication | `your-service-secret-here` |
| `X-Service-Origin` | Service identifier | `auth`, `orders`, `ridesharing`, `products` |
| `X-Correlation-Id` | Distributed tracing ID | `trace-abc-123` |

### Client Example (Listo.Orders calling Listo.Notification)

```csharp
using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Add("X-Service-Secret", ordersServiceSecret);
httpClient.DefaultRequestHeaders.Add("X-Service-Origin", "orders");
httpClient.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

var response = await httpClient.PostAsJsonAsync(
    "https://notification.listoexpress.com/api/v1/internal/notifications/queue",
    notificationRequest);
```

### Service Secret Generation

**Best Practices:**
- Use cryptographically secure random generators
- Minimum 32 characters (256 bits)
- Store in Azure Key Vault
- Rotate every 90 days

**PowerShell Generation:**

```powershell
# Generate a secure service secret
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$secret = [Convert]::ToBase64String($bytes)
Write-Host $secret
```

### Key Rotation Strategy

1. **Add new secret** to Key Vault with version suffix (e.g., `AUTH_SERVICE_SECRET_V2`)
2. **Update consuming services** to use new secret
3. **Configure both secrets** in Notification service (grace period: 7 days)
4. **Monitor logs** for usage of old secret
5. **Remove old secret** after grace period

---

## 3. HTTPS & HSTS Configuration

### Program.cs Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Enforce HTTPS in production
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

var app = builder.Build();

// Apply HTTPS redirection and HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

### HSTS Header Response

```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

---

## 4. Input Validation & Request Limits

### Request Validation Middleware

**Enforces:**
- Content-Type validation (JSON, form-data only)
- Request body size limit: **5 MB**
- Header value length limit: **8 KB**
- Required correlation headers

**Registration:**

```csharp
app.UseRequestValidation(); // Before authentication
app.UseAuthentication();
app.UseAuthorization();
```

### Rate Limiting Configuration

**Global Limits:**
- 100 requests per 15 minutes per IP address
- No queueing (immediate rejection)

**Channel-Specific Limits:**
- SMS: 10 per minute
- Email: 30 per minute
- Push: 60 per minute
- In-App: 100 per minute

### Response Headers

**Success:**
```
X-Correlation-Id: trace-abc-123
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1234567890
```

**Rate Limit Exceeded (429):**
```
HTTP/1.1 429 Too Many Requests
Retry-After: 900
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1234567890

{
  "error": "TooManyRequests",
  "message": "Rate limit exceeded. Retry after 900 seconds.",
  "retryAfter": 900
}
```

---

## 5. Authorization Policies

### Role-Based Access Control

```csharp
builder.Services.AddAuthorization(options =>
{
    // Admin-only endpoints
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    // Support and Admin
    options.AddPolicy("SupportAccess", policy =>
        policy.RequireRole("Admin", "Support"));

    // Service-to-Service
    options.AddPolicy("ServiceOnly", policy =>
        policy.RequireRole("Service"));

    // Manage templates
    options.AddPolicy("ManageTemplates", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("permissions", "notifications.templates.write"));
});
```

### Usage

```csharp
[Authorize(Policy = "AdminOnly")]
[HttpGet("admin/cost-tracking")]
public async Task<IActionResult> GetCostTracking() { }

[Authorize(Policy = "ServiceOnly")]
[HttpPost("internal/notifications/queue")]
public async Task<IActionResult> QueueNotification() { }
```

---

## 6. Security Checklist

### Pre-Production

- [ ] JWT secret key stored in Azure Key Vault
- [ ] Service secrets stored in Azure Key Vault
- [ ] HTTPS enforced (no HTTP)
- [ ] HSTS enabled with 1-year max-age
- [ ] CORS configured with explicit origins (no wildcards)
- [ ] Request validation middleware enabled
- [ ] Rate limiting configured per channel
- [ ] Security headers configured (CSP, X-Content-Type-Options, X-Frame-Options)
- [ ] API versioning enabled (`/api/v1`)
- [ ] Correlation IDs required and propagated

### Runtime Monitoring

- [ ] Failed authentication attempts logged
- [ ] Invalid service secrets logged with alerts
- [ ] Rate limit violations monitored
- [ ] Abnormal request patterns detected
- [ ] Service secret rotation schedule active

---

## 7. Testing Authentication

### Test JWT Authentication

```bash
# Get JWT token from Listo.Auth
TOKEN=$(curl -X POST https://auth.listoexpress.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}' \
  | jq -r '.token')

# Call Notification API with JWT
curl -X GET https://notification.listoexpress.com/api/v1/notifications \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Correlation-Id: test-123"
```

### Test Service Secret Authentication

```bash
# Call internal endpoint with service secret
curl -X POST https://notification.listoexpress.com/api/v1/internal/notifications/queue \
  -H "Content-Type: application/json" \
  -H "X-Service-Secret: your-service-secret" \
  -H "X-Service-Origin: orders" \
  -H "X-Correlation-Id: test-456" \
  -d '{
    "userId": "user-123",
    "channel": "email",
    "templateKey": "order_confirmed"
  }'
```

---

## 8. Troubleshooting

### Common Issues

**401 Unauthorized - Missing JWT**
```
Solution: Include "Authorization: Bearer {token}" header
```

**401 Unauthorized - Invalid Service Secret**
```
Solution: Verify X-Service-Secret matches Key Vault value
```

**429 Too Many Requests**
```
Solution: Implement exponential backoff, respect Retry-After header
```

**415 Unsupported Media Type**
```
Solution: Set Content-Type to "application/json"
```

---

**Next Steps:**
- Configure Azure Key Vault integration
- Set up service secret rotation schedule
- Implement security monitoring dashboards
- Configure Application Insights alerts
