## 1. Purpose & Overview

**Listo Notification API** is a comprehensive notification management service for the ListoExpress platform that handles multi-channel notification delivery with real-time messaging capabilities. It provides:

- **Multi-channel delivery**: SMS, Email, Push Notifications, and In-app messages
- **Real-time messaging**: SignalR-based instant messaging, presence tracking, typing indicators, and read receipts
- **Template management**: Notification template rendering and customization
- **Rate limiting & budget enforcement**: Control notification volume and costs
- **Audit logging**: Track all notification activities for compliance

---

## 2. Architecture & Technology Stack

### **Architecture Pattern**
- **Layered/N-Tier Architecture**: API → Application Layer → Infrastructure Layer → Data Layer
- **Service-Oriented**: Modular service design (NotificationService, TemplateRenderingService, RateLimiterService, etc.)
- **Real-Time Hub Pattern**: SignalR for bidirectional communication

### **Technology Stack**

| Component | Technology |
|-----------|-----------|
| **Framework** | ASP.NET Core (minimal API with controllers) |
| **Authentication** | JWT Bearer Tokens |
| **Authorization** | Role-based policies (RBAC) |
| **Database** | SQL Server (Entity Framework Core) |
| **Caching/Real-time** | Redis (token bucket limiting, SignalR backplane, presence tracking) |
| **Real-time Communication** | SignalR |
| **File Storage** | Azure Blob Storage (attachments) |
| **Validation** | FluentValidation |
| **API Documentation** | Swagger/OpenAPI |
| **Rate Limiting** | Built-in ASP.NET Core Rate Limiting |
| **Security** | HTTPS, HSTS, JWT with signing key validation |

---

## 3. External Service Dependencies

| Service | Purpose | Provider |
|---------|---------|----------|
| **SMS Provider** | Send SMS notifications | Twilio |
| **Email Provider** | Send email notifications | SendGrid |
| **Push Notifications** | Mobile push notifications | Firebase Cloud Messaging (FCM) |
| **Auth Service** | Device token lookup & user validation | Listo.Auth (internal service) |
| **Blob Storage** | Store notification attachments | Azure Blob Storage |
| **Redis** | Caching, rate limiting, SignalR backplane | Redis (self-hosted or managed) |
| **SQL Server** | Persistent data storage | SQL Server |

---

## 4. Authentication & Authorization

### **Authentication Method**
- **JWT Bearer Token** (RFC 7519)
- Token validation includes:
  - Issuer validation
  - Audience validation
  - Lifetime validation (expiration)
  - HMAC signature verification with SecretKey
  - Zero clock skew tolerance

### **Authorization Policies**

| Policy Name | Required Roles | Claims | Purpose |
|------------|---|---|---|
| **AdminOnly** | Admin | - | Cost tracking, template management, rate limit overrides |
| **SupportAccess** | Admin, Support | - | View audit logs, manage conversations |
| **ServiceOnly** | Service | - | Service-to-service internal endpoints |
| **ManageTemplates** | Admin | `permissions: notifications.templates.write` | Create, update, delete templates |
| **ManageBudgets** | Admin | `permissions: notifications.budgets.write` | Manage notification budgets/rate limits |

### **SignalR Hub Authentication**
- JWT token passed via query string: `?access_token=<token>`
- Tokens validated same as HTTP Bearer scheme

---

## 5. Project Structure

````
Listo.Notification/
├── Listo.Notification.API/
│   ├── Program.cs                    # Configuration & DI setup
│   ├── Controllers/                  # API endpoints
│   ├── Hubs/
│   │   ├── NotificationHub.cs       # Real-time notifications
│   │   └── MessagingHub.cs          # Real-time messaging
│   ├── Middleware/                   # Custom middleware
│   ├── Filters/                      # Authorization filters
│   └── Filters/SignalRRateLimitFilter.cs
├── Listo.Notification.Application/
│   ├── Interfaces/
│   │   ├── INotificationService.cs
│   │   ├── ITemplateRenderingService.cs
│   │   ├── IRateLimiterService.cs
│   │   ├── IPresenceTrackingService.cs
│   │   ├── IReadReceiptService.cs
│   │   └── ITypingIndicatorService.cs
│   ├── Services/
│   │   ├── NotificationService.cs
│   │   ├── TemplateRenderingService.cs
│   │   ├── RateLimitingService.cs
│   │   └── BudgetEnforcementService.cs
│   ├── Validators/
│   │   └── SendNotificationRequestValidator.cs
│   └── DTOs/                         # Request/Response models
├── Listo.Notification.Infrastructure/
│   ├── Data/
│   │   └── NotificationDbContext.cs
│   ├── Repositories/
│   │   ├── INotificationRepository.cs
│   │   ├── ITemplateRepository.cs
│   │   └── Implementations/
│   ├── Services/
│   │   ├── RedisTokenBucketLimiter.cs
│   │   ├── PresenceTrackingService.cs
│   │   ├── ReadReceiptService.cs
│   │   ├── TypingIndicatorService.cs
│   │   └── AttachmentStorageService.cs
│   ├── Providers/
│   │   ├── TwilioSmsProvider.cs
│   │   ├── SendGridEmailProvider.cs
│   │   └── FcmPushProvider.cs
│   ├── ExternalServices/
│   │   └── AuthServiceClient.cs
│   └── Configuration/
└── Listo.Notification.Infrastructure.Data/
    └── Migrations/
````

---

## 6. Rate Limiting Configuration

| Policy | Limit | Window | Purpose |
|--------|-------|--------|---------|
| **Global** | 100 requests | 15 minutes | Default limit per IP |
| **SMS** | 10 SMS | 1 minute | Per-IP SMS throttling |
| **Email** | 30 emails | 1 minute | Per-IP email throttling |
| **Push** | 60 notifications | 1 minute | Per-IP push throttling |
| **In-app** | 120 messages | 1 minute | Per-IP in-app message throttling |

---

