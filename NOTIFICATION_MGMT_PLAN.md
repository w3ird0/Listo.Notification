# Notification & Communication Service API Implementation Plan

This plan details the architecture, implementation steps, tools, and best practices for building a **shared Notification & Communication Service** for the ListoExpress ecosystem using **.NET 9**, **SQL Server**, **Azure Service Bus**, **Azure Functions**, and deploying as a container on **Azure**.

This service integrates with **Listo.Auth**, **Listo.Orders**, **Listo.RideSharing**, and other microservices to provide unified notification delivery across multiple channels (Push, SMS, Email, In-App Messaging).

**Related Documentation:**
- API Endpoints: [`notification_api_endpoints.md`](./notification_api_endpoints.md)
- Auth Service: [Listo.Auth Documentation](../Listo.Auth/docs/)
- Orders Service: [Listo.Orders Documentation](../Listo.Orders/docs/)
- RideSharing Service: [Listo.RideSharing Documentation](../Listo.RideSharing/)

---

## Table of Contents

1. [Requirements Analysis](#1-requirements-analysis)
2. [Technology Stack](#2-technology-stack)
3. [Service Integration & Architecture](#3-service-integration--architecture)
4. [Data Modeling & Database Schema](#4-data-modeling--database-schema)
5. [Authentication & Authorization](#5-authentication--authorization)
6. [Service-Specific Event Mappings](#6-service-specific-event-mappings)
7. [Cost Management & Rate Limiting](#7-cost-management--rate-limiting)
8. [Notification Delivery Strategy](#8-notification-delivery-strategy)
9. [Real-Time Messaging with SignalR](#9-real-time-messaging-with-signalr)
10. [API Implementation](#10-api-implementation)
11. [Validation & Error Handling](#11-validation--error-handling)
12. [File & Image Upload Handling](#12-file--image-upload-handling)
13. [Testing Strategy](#13-testing-strategy)
14. [Azure Functions Configuration](#14-azure-functions-configuration)
15. [Configuration Management](#15-configuration-management)
16. [Containerization](#16-containerization)
17. [Azure Deployment](#17-azure-deployment)
18. [Monitoring & Logging](#18-monitoring--logging)
19. [API Documentation](#19-api-documentation)
20. [CI/CD Pipeline](#20-cicd-pipeline)
21. [Security](#21-security)
22. [GDPR & Compliance](#22-gdpr--compliance)
23. [Clean Architecture Implementation](#23-clean-architecture-implementation)
24. [Maintenance & Future Enhancements](#24-maintenance--future-enhancements)

---

## 1. Requirements Analysis

### Core Requirements

- **Review API Spec:** Thoroughly review [`notification_api_endpoints.md`](./notification_api_endpoints.md) for endpoints, authentication, data models, and business logic.
- **Identify Resources:** Notifications (Push, SMS, Email, In-App), Devices, Templates, Preferences, Conversations, Analytics, Health.
- **Multi-Channel Support:** Email, SMS, Push Notifications, Real-time In-App Messaging.
- **Service Integration:** Integrate with Listo.Auth, Listo.Orders, Listo.RideSharing via REST APIs, Azure Service Bus, and event-driven patterns.

### Authentication & Authorization

- **Client Authentication:** JWT Bearer tokens issued by Listo.Auth
- **Service-to-Service:** Shared secrets stored in Azure Key Vault (X-Service-Secret header)
- **Roles:** Customer, Driver, Support, Admin (managed by Listo.Auth)

### Delivery Requirements

- **Synchronous:** Driver assignment alerts (push + SignalR) must be synchronous
- **Asynchronous:** All other notifications queued via Azure Service Bus
- **Retry Policy:** Exponential backoff with jitter, max 6 attempts, no dead-letter queues
- **Rate Limiting:** Per-user and per-service quotas with cost management

### Compliance

- **GDPR:** Data retention, audit trails, user data export and deletion
- **PII Encryption:** Encrypt email, phone numbers, device tokens at rest
- **Audit Logging:** Track all notification activities for compliance

### Default Configuration Values

- **Retention:**
  - Notification content: 90 days
  - In-app chat (Support): 180 days
  - In-app chat (Customer-Driver): 30 days
  - Audit logs: 13 months
  - Queue rows: Purged 30 days after completion
- **Retry Policy:**
  - Base delay: 5 seconds
  - Backoff factor: 2
  - Jitter: Enabled
  - Max attempts: 6
- **Rate Limits:**
  - Per-user per channel: 60/hour with burst of 20
  - Per-service daily caps: Email 50k, SMS 10k, Push 200k, In-app unlimited
  - Redis token bucket implementation
- **Budget Thresholds:**
  - Alert at 80% and 100% per month per service and channel
  - Block non-critical notifications beyond budget (admin override available)
- **Localization:**
  - Default locale: en-US
  - Template fallback strategy
- **Idempotency:**
  - X-Idempotency-Key required on create/send POSTs
  - Uniqueness window: 24 hours per serviceOrigin
- **Observability:**
  - OpenTelemetry tracing with W3C traceparent
  - X-Correlation-Id headers required

---

## 2. Technology Stack

### Backend Framework

- **.NET 9** (ASP.NET Core Web API)
- **NO MediatR** (per architectural guidelines - use direct service injection)

### Data Layer

- **Database:** Microsoft SQL Server (Dedicated Azure SQL Database)
- **ORM:** Entity Framework Core 9.0
- **Migration Tool:** EF Core Migrations

### Azure Services

- **Azure Service Bus:** Message queues and pub/sub topics
  - Queues: `listo-notifications-queue`, `listo-notifications-retry`, `listo-notifications-priority`
  - Topic: `listo-notifications-events` with subscriptions (auth, orders, ridesharing)
- **Azure Functions:** Background job processing
  - Scheduled notification runner
  - Retry processor
  - Cost and budget calculator
  - Data retention cleaner
- **Azure SignalR Service:** Real-time in-app messaging
- **Azure Key Vault:** Secrets management (provider credentials, service secrets)
- **Azure Container Apps / AKS:** Container hosting
- **Azure Application Insights:** Monitoring and logging
- **Azure Blob Storage:** File uploads for messaging

### Caching & Rate Limiting

- **Azure Cache for Redis:** Rate limiting (token bucket), presence, typing indicators

### External Providers

- **Push Notifications:** Firebase Cloud Messaging (FCM)
- **SMS Service:** Twilio
- **Email Service:** SendGrid or Azure Communication Services

### Development Tools

- **Validation:** FluentValidation
- **API Documentation:** Swashbuckle (Swagger/OpenAPI)
- **Testing:** xUnit, Moq, FluentAssertions, Testcontainers
- **Logging:** Serilog with structured logging
- **CI/CD:** GitHub Actions or Azure DevOps
- **Containerization:** Docker
- **IaC:** Bicep or Terraform

---

## 3. Service Integration & Architecture

### 3.1. Integration Patterns

The Listo.Notification service supports **three primary integration patterns** to accommodate different use cases:

#### Direct REST API Calls

**Use Case:** Synchronous operations requiring immediate feedback

**Authentication:**
- **Client Requests:** `Authorization: Bearer {jwt_token}` (issued by Listo.Auth)
- **Service-to-Service:** `X-Service-Secret: {shared_secret}` (from Azure Key Vault)

**Required Headers:**
```http
Authorization: Bearer {jwt_token}  # OR X-Service-Secret for services
Content-Type: application/json
X-Idempotency-Key: {unique_key}    # Required for POST operations
X-Correlation-Id: {trace_id}       # Required for tracing
traceparent: {w3c_trace_context}   # OpenTelemetry tracing
```

**Example:** Driver assignment push notification
```http
POST /api/v1/internal/notifications/queue
X-Service-Secret: {secret}
X-Idempotency-Key: orders-driver-assign-12345
X-Correlation-Id: trace-abc-123

{
  "userId": "driver-uuid-456",
  "serviceOrigin": "orders",
  "channel": "push",
  "templateKey": "driver_assignment",
  "priority": "high",
  "synchronous": true,
  "data": {
    "orderId": "ORD-001",
    "customerName": "John Doe",
    "pickupAddress": "123 Main St"
  },
  "encryptedEmail": null,
  "encryptedPhoneNumber": null,
  "encryptedFirebaseToken": "encrypted_fcm_token"
}
```

#### Azure Service Bus Integration

**Use Case:** Asynchronous, reliable message delivery with retry capabilities

**Queues:**
- `listo-notifications-queue`: Standard priority notifications
- `listo-notifications-priority`: High-priority time-sensitive notifications
- `listo-notifications-retry`: Failed notifications for retry processing

**Topic & Subscriptions:**
- Topic: `listo-notifications-events`
- Subscriptions:
  - `auth-notifications`: Email verification, password reset, 2FA
  - `orders-notifications`: Order confirmations, status updates
  - `ridesharing-notifications`: Ride bookings, driver assignments

**Message Envelope Format:**
```json
{
  "eventId": "evt-uuid-789",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "NotificationRequested",
  "serviceOrigin": "orders",
  "userId": "user-uuid-123",
  "correlationId": "trace-abc-123",
  "idempotencyKey": "orders-status-update-456",
  "priority": "normal",
  "channels": ["email", "push"],
  "templateKey": "order_status_updated",
  "data": {
    "orderId": "ORD-001",
    "newStatus": "preparing",
    "estimatedTime": "30 minutes"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

**Application Properties (Service Bus):**
```json
{
  "ServiceOrigin": "orders",
  "MessageType": "NotificationRequested",
  "Priority": "normal",
  "CorrelationId": "trace-abc-123"
}
```

#### Event-Driven Architecture

**Use Case:** Loosely coupled microservice communication via domain events

**Pattern:**
1. Service publishes domain event to Service Bus topic
2. Listo.Notification subscribes to relevant events
3. Events are mapped to notification templates
4. Notifications are processed based on user preferences

**Example Event Flow:**
```
Listo.Orders → OrderStatusChanged event
     ↓
Service Bus Topic (listo-notifications-events)
     ↓
Listo.Notification Subscription (orders-notifications)
     ↓
Map to template: order_status_updated
     ↓
Check user preferences → Send via enabled channels
```

### 3.2. Service Origin Tracking

All notifications include `serviceOrigin` to enable:
- **Cost Attribution:** Track costs per service
- **Rate Limiting:** Apply service-specific quotas
- **Analytics:** Generate per-service reports
- **Compliance:** Audit trail scoped by service

**Valid Service Origins:**
- `auth`
- `orders`
- `ridesharing`
- `products`
- `system` (for admin/platform notifications)

### 3.3. Sequence Diagrams

#### Synchronous Driver Assignment (Critical Path)

```mermaid
sequenceDiagram
    participant Orders as Listo.Orders
    participant NotifAPI as Notification API
    participant FCM as Firebase FCM
    participant SignalR as SignalR Hub
    participant Driver as Driver App

    Orders->>NotifAPI: POST /internal/notifications/queue<br/>(synchronous=true)
    NotifAPI->>NotifAPI: Validate & Decrypt
    par Parallel Delivery
        NotifAPI->>FCM: Send Push Notification
        FCM-->>Driver: Push Delivered
    and
        NotifAPI->>SignalR: Broadcast to Driver
        SignalR-->>Driver: Real-time Update
    end
    NotifAPI->>NotifAPI: Log to Database
    NotifAPI-->>Orders: 200 OK (both delivered)
```

#### Asynchronous Order Status Update

```mermaid
sequenceDiagram
    participant Orders as Listo.Orders
    participant ServiceBus as Azure Service Bus
    participant NotifFunc as Notification Processor<br/>(Azure Function)
    participant SendGrid as SendGrid
    participant Customer as Customer

    Orders->>ServiceBus: Publish OrderStatusChanged event
    Orders-->>Orders: Continue processing
    ServiceBus->>NotifFunc: Trigger Function
    NotifFunc->>NotifFunc: Map event to template<br/>Check user preferences
    NotifFunc->>SendGrid: Send Email
    SendGrid-->>Customer: Email Delivered
    SendGrid->>NotifFunc: Webhook: Delivered
    NotifFunc->>NotifFunc: Update status in DB
```

### 3.4. Idempotency & Correlation

**Idempotency:**
- **Header:** `X-Idempotency-Key` (required for all POST operations)
- **Uniqueness Window:** 24 hours per `serviceOrigin`
- **Behavior:** Duplicate requests return original response (200 OK)
- **Storage:** Redis cache with 24-hour TTL

**Correlation:**
- **Header:** `X-Correlation-Id` (required, auto-generated if missing)
- **Purpose:** End-to-end tracing across microservices
- **Format:** UUID v4 or W3C traceparent
- **Propagation:** Passed to all downstream calls (FCM, Twilio, SendGrid)

---

## 3. Solution Architecture

- **Project Structure:** Use Clean Architecture (Domain, Application, Infrastructure, API layers).
- **API Versioning:** Prefix all routes with `/api/v1`.
- **Configuration:** Use appsettings and environment variables for DB, service credentials, and secrets.

---

## 4. Data Modeling & Database Schema

**Status:** ✅ **COMPLETE** (as of 2025-01-20)

**Referenced By:** Section 7 (Rate Limiting → RateLimiting table), Section 7 (Cost Management → CostTracking table), Section 8 (Templates → Templates table), Section 9 (Preferences → Preferences table)

### Overview

**Database:** Dedicated SQL Server instance (not shared with other services)  
**Encryption:** PII encrypted at rest using AES-256-GCM with per-row IV  
**Key Management:** Encryption keys stored in Azure Key Vault  
**TDE:** Transparent Data Encryption enabled on SQL Server

### 4.1. Notifications Table

**Purpose:** Immutable audit log of all sent notifications

| Field              | Type           | Nullable | Description                               |
|--------------------|----------------|----------|-------------------------------------------|
| Id                 | GUID (PK)      | No       | Notification ID                           |
| TenantId           | GUID           | No       | Tenant identifier for multi-tenancy       |
| UserId             | string         | Yes      | Target user (null for broadcast)          |
| ServiceOrigin      | string         | No       | auth, orders, ridesharing, products       |
| Channel            | string         | No       | push, sms, email, inApp                   |
| TemplateKey        | string         | No       | Reference to template                     |
| Status             | string         | No       | queued, sent, delivered, opened, failed   |
| Priority           | string         | No       | high, normal, low                         |
| ScheduledAt        | datetime       | Yes      | For scheduled notifications               |
| SentAt             | datetime       | Yes      | Actual send timestamp                     |
| ProviderMessageId  | string         | Yes      | External provider message ID              |
| ErrorCode          | string         | Yes      | Provider error code if failed             |
| ErrorMessage       | string         | Yes      | Error details                             |
| CorrelationId      | string         | No       | Tracing correlation ID                    |
| CreatedAt          | datetime       | No       | Record creation timestamp                 |
| UpdatedAt          | datetime       | No       | Last update timestamp                     |

**Indexes:**
- `IX_Notifications_TenantId_UserId_CreatedAt` (TenantId, UserId, CreatedAt DESC)
- `IX_Notifications_TenantId_ServiceOrigin_CreatedAt` (TenantId, ServiceOrigin, CreatedAt DESC)
- `IX_Notifications_Status_ScheduledAt` (Status, ScheduledAt) WHERE ScheduledAt IS NOT NULL
- `IX_Notifications_CorrelationId` (CorrelationId)
- `IX_Notifications_TenantId` (TenantId)

---

### 4.2. NotificationQueue Table

**Purpose:** Transient queue for notifications pending delivery with encrypted PII

| Field                    | Type           | Nullable | Description                               |
|--------------------------|----------------|----------|-------------------------------------------|
| QueueId                  | GUID (PK)      | No       | Queue record ID                           |
| UserId                   | string         | Yes      | Target user (null for broadcast)          |
| ServiceOrigin            | string         | No       | auth, orders, ridesharing, products       |
| Channel                  | string         | No       | push, sms, email, inApp                   |
| TemplateKey              | string         | No       | Template to use for rendering             |
| PayloadJson              | nvarchar(max)  | No       | Template variables and metadata as JSON   |
| EncryptedEmail           | varbinary(512) | Yes      | AES-256-GCM encrypted email               |
| EncryptedPhoneNumber     | varbinary(512) | Yes      | AES-256-GCM encrypted phone number        |
| EncryptedFirebaseToken   | varbinary(512) | Yes      | AES-256-GCM encrypted FCM token           |
| EmailHash                | varchar(64)    | Yes      | SHA-256 hash for deduplication            |
| PhoneHash                | varchar(64)    | Yes      | SHA-256 hash for deduplication            |
| PreferredLocale          | varchar(10)    | No       | e.g., en-US, es-MX                        |
| ScheduledAt              | datetime       | Yes      | For scheduled delivery                    |
| Attempts                 | int            | No       | Retry attempt count (default: 0)          |
| NextAttemptAt            | datetime       | Yes      | Calculated next retry time                |
| LastErrorCode            | string         | Yes      | Last failure error code                   |
| LastErrorMessage         | string         | Yes      | Last failure error message                |
| CorrelationId            | string         | No       | Tracing correlation ID                    |
| CreatedAt                | datetime       | No       | Queue entry creation timestamp            |

**Indexes:**
- `IX_NotificationQueue_ScheduledAt_Attempts` (ScheduledAt, Attempts) WHERE NextAttemptAt IS NOT NULL
- `IX_NotificationQueue_EmailHash` (EmailHash) WHERE EmailHash IS NOT NULL
- `IX_NotificationQueue_CreatedAt` (CreatedAt) for cleanup

**Encryption Notes:**
- Each row uses a unique IV (Initialization Vector) stored with encrypted data
- Encryption key from Azure Key Vault (key rotation supported)
- Hashes used for duplicate detection without decryption

**Cleanup:**
- Records purged 30 days after successful delivery or final failure

---

### 4.3. RetryPolicy Table

**Purpose:** Configurable retry policies per service origin and channel

| Field               | Type     | Nullable | Description                               |
|---------------------|----------|----------|-------------------------------------------|
| PolicyId            | GUID (PK)| No       | Policy ID                                 |
| ServiceOrigin       | string   | No       | auth, orders, ridesharing, products, *    |
| Channel             | string   | No       | push, sms, email, inApp, *                |
| MaxAttempts         | int      | No       | Maximum retry attempts (default: 6)       |
| BaseDelaySeconds    | int      | No       | Initial retry delay (default: 5)          |
| BackoffFactor       | decimal  | No       | Exponential backoff multiplier (default: 2.0) |
| JitterMs            | int      | No       | Random jitter in ms (default: 1000)       |
| TimeoutSeconds      | int      | No       | Per-attempt timeout (default: 30)         |
| Enabled             | bool     | No       | Is policy active (default: true)          |

**Unique Constraint:** (ServiceOrigin, Channel)

**Default Policies (Seeded):**
```sql
INSERT INTO RetryPolicy (PolicyId, ServiceOrigin, Channel, MaxAttempts, BaseDelaySeconds, BackoffFactor, JitterMs, TimeoutSeconds, Enabled)
VALUES
  (NEWID(), '*', '*', 6, 5, 2.0, 1000, 30, 1),              -- Default for all
  (NEWID(), 'orders', 'push', 3, 2, 2.0, 500, 15, 1),       -- Faster for driver assignment
  (NEWID(), 'auth', 'sms', 4, 3, 2.0, 500, 20, 1);          -- OTP/2FA retry policy
```

**Retry Calculation Logic:**
```
NextAttemptDelay = (BaseDelaySeconds * (BackoffFactor ^ Attempts)) + Random(0, JitterMs)
```

---

### 4.4. CostTracking Table

**Purpose:** Per-message cost tracking for budget management (per-tenant and per-service)

| Field               | Type           | Nullable | Description                               |
|---------------------|----------------|----------|-------------------------------------------|
| CostId              | GUID (PK)      | No       | Cost record ID                            |
| TenantId            | GUID           | No       | Tenant identifier for multi-tenancy       |
| ServiceOrigin       | string         | No       | auth, orders, ridesharing, products       |
| Channel             | string         | No       | push, sms, email, inApp                   |
| Provider            | string         | No       | fcm, twilio, sendgrid, aws_sns, acs       |
| UnitCostMicros      | bigint         | No       | Cost per unit in micros (1/1,000,000)     |
| Currency            | varchar(3)     | No       | USD, EUR, etc.                            |
| MessageId           | GUID           | Yes      | Reference to Notifications table          |
| UsageUnits          | int            | No       | Number of units (segments for SMS)        |
| TotalCostMicros     | bigint         | No       | UsageUnits * UnitCostMicros               |
| OccurredAt          | datetime       | No       | Cost incurred timestamp                   |

**Indexes:**
- `IX_CostTracking_TenantId_ServiceOrigin_OccurredAt` (TenantId, ServiceOrigin, OccurredAt DESC)
- `IX_CostTracking_TenantId_Channel_OccurredAt` (TenantId, Channel, OccurredAt DESC)
- `IX_CostTracking_ServiceOrigin_OccurredAt` (ServiceOrigin, OccurredAt DESC)
- `IX_CostTracking_TenantId` (TenantId)

**Monthly Rollup Views:**

```sql
-- Per-Tenant Monthly Summary
CREATE VIEW CostMonthlySummaryByTenant AS
SELECT 
    TenantId,
    ServiceOrigin,
    Channel,
    YEAR(OccurredAt) AS Year,
    MONTH(OccurredAt) AS Month,
    Currency,
    SUM(TotalCostMicros) / 1000000.0 AS TotalCost,
    COUNT(*) AS MessageCount,
    AVG(TotalCostMicros) / 1000000.0 AS AvgCostPerMessage
FROM CostTracking
GROUP BY TenantId, ServiceOrigin, Channel, YEAR(OccurredAt), MONTH(OccurredAt), Currency;

-- Per-Service Monthly Summary (across all tenants)
CREATE VIEW CostMonthlySummaryByService AS
SELECT 
    ServiceOrigin,
    Channel,
    YEAR(OccurredAt) AS Year,
    MONTH(OccurredAt) AS Month,
    Currency,
    SUM(TotalCostMicros) / 1000000.0 AS TotalCost,
    COUNT(*) AS MessageCount,
    AVG(TotalCostMicros) / 1000000.0 AS AvgCostPerMessage
FROM CostTracking
GROUP BY ServiceOrigin, Channel, YEAR(OccurredAt), MONTH(OccurredAt), Currency;
```

**Cost Examples:**
- FCM Push: $0 (free, but track for analytics)
- Twilio SMS (US): ~$0.0079/message → 7900 micros
- SendGrid Email: ~$0.00095/email → 950 micros

---

### 4.5. RateLimiting Table

**Purpose:** Configurable rate limits per service and user (tenant-scoped with maximum caps)

| Field                    | Type     | Nullable | Description                               |
|--------------------------|----------|----------|-------------------------------------------|
| ConfigId                 | GUID (PK)| No       | Configuration ID                          |
| TenantId                 | GUID     | Yes      | Tenant ID (null for global defaults)      |
| ServiceOrigin            | string   | No       | auth, orders, ridesharing, products, *    |
| Channel                  | string   | No       | push, sms, email, inApp, *                |
| PerUserWindowSeconds     | int      | No       | Time window for per-user limit (3600)     |
| PerUserMax               | int      | No       | Max per user in window (60)               |
| PerUserMaxCap            | int      | No       | Absolute maximum cap (cannot be exceeded) |
| PerServiceWindowSeconds  | int      | No       | Time window for per-service (86400)       |
| PerServiceMax            | int      | No       | Max per service in window (50000)         |
| PerServiceMaxCap         | int      | No       | Absolute maximum cap for service          |
| BurstSize                | int      | No       | Allowed burst above limit (20)            |
| Enabled                  | bool     | No       | Is limit enforced (default: true)         |

**Unique Constraint:** (TenantId, ServiceOrigin, Channel)

**Default Limits (Seeded):**
```sql
INSERT INTO RateLimiting (ConfigId, ServiceOrigin, Channel, PerUserWindowSeconds, PerUserMax, PerServiceWindowSeconds, PerServiceMax, BurstSize, Enabled)
VALUES
  (NEWID(), '*', 'email', 3600, 60, 86400, 50000, 20, 1),
  (NEWID(), '*', 'sms', 3600, 60, 86400, 10000, 20, 1),
  (NEWID(), '*', 'push', 3600, 60, 86400, 200000, 20, 1),
  (NEWID(), '*', 'inApp', 3600, 1000, 86400, 999999999, 100, 1); -- Unlimited for in-app
```

**Implementation:** Redis token bucket pattern

---

### 4.6. AuditLog Table

**Purpose:** Immutable audit trail for compliance (GDPR, SOC 2)

| Field          | Type           | Nullable | Description                               |
|----------------|----------------|----------|-------------------------------------------|
| AuditId        | GUID (PK)      | No       | Audit record ID                           |
| Action         | string         | No       | created, updated, deleted, sent, delivered|
| EntityType     | string         | No       | notification, template, preference, user  |
| EntityId       | GUID           | No       | ID of the affected entity                 |
| UserId         | string         | Yes      | User who performed action (null=system)   |
| ServiceOrigin  | string         | Yes      | auth, orders, ridesharing, system         |
| ActorType      | string         | No       | user, service, system, admin              |
| IpAddress      | string         | Yes      | Source IP address                         |
| UserAgent      | string         | Yes      | Browser/client user agent                 |
| BeforeJson     | nvarchar(max)  | Yes      | Entity state before change (JSON)         |
| AfterJson      | nvarchar(max)  | Yes      | Entity state after change (JSON)          |
| OccurredAt     | datetime       | No       | Audit event timestamp (UTC)               |

**Indexes:**
- `IX_AuditLog_EntityType_EntityId` (EntityType, EntityId, OccurredAt DESC)
- `IX_AuditLog_UserId_OccurredAt` (UserId, OccurredAt DESC) WHERE UserId IS NOT NULL
- `IX_AuditLog_OccurredAt` (OccurredAt DESC)

**Retention:** 13 months (for compliance), then archived or deleted

---

### 4.7. Templates Table

**Purpose:** Notification templates with versioning and localization

| Field          | Type           | Nullable | Description                               |
|----------------|----------------|----------|-------------------------------------------|
| TemplateId     | GUID (PK)      | No       | Template ID                               |
| TemplateKey    | string         | No       | Unique key (e.g., order_confirmed)        |
| Channel        | string         | No       | push, sms, email, inApp                   |
| Locale         | varchar(10)    | No       | en-US, es-MX, fr-FR, etc.                 |
| Subject        | string         | Yes      | Email subject or push title               |
| Body           | nvarchar(max)  | No       | Template body with {{variables}}          |
| Variables      | nvarchar(max)  | No       | JSON array of expected variables          |
| Version        | int            | No       | Template version number                   |
| IsActive       | bool           | No       | Is template active (default: true)        |
| CreatedAt      | datetime       | No       | Template creation timestamp               |
| UpdatedAt      | datetime       | No       | Last update timestamp                     |

**Unique Constraint:** (TemplateKey, Channel, Locale, Version)

**Indexes:**
- `IX_Templates_TemplateKey_Channel_Locale` (TemplateKey, Channel, Locale) WHERE IsActive = 1

**Localization Fallback Strategy:**
1. Try exact locale match (e.g., en-US)
2. Try language match (e.g., en)
3. Fall back to en-US (default)

**Example Template:**
```json
{
  "templateKey": "order_confirmed",
  "channel": "email",
  "locale": "en-US",
  "subject": "Order Confirmed - {{orderId}}",
  "body": "Hi {{customerName}}, your order {{orderId}} has been confirmed! Expected delivery: {{deliveryTime}}.",
  "variables": ["customerName", "orderId", "deliveryTime"],
  "version": 1,
  "isActive": true
}
```

---

### 4.8. Preferences Table

**Purpose:** User notification preferences and quiet hours (tenant-scoped)

| Field          | Type           | Nullable | Description                               |
|----------------|----------------|----------|-------------------------------------------|
| PreferenceId   | GUID (PK)      | No       | Preference ID                             |
| TenantId       | GUID           | No       | Tenant identifier for multi-tenancy       |
| UserId         | string         | No       | User reference (from Listo.Auth)          |
| Channel        | string         | No       | push, sms, email, inApp, *                |
| IsEnabled      | bool           | No       | Is channel enabled (default: true)        |
| QuietHours     | nvarchar(max)  | Yes      | JSON: {enabled, startTime, endTime, tz}   |
| Topics         | nvarchar(max)  | Yes      | JSON array of enabled topics              |
| Locale         | varchar(10)    | No       | Preferred locale (default: en-US)         |
| UpdatedAt      | datetime       | No       | Last update timestamp                     |

**Unique Constraint:** (TenantId, UserId, Channel)

**Indexes:**
- `IX_Preferences_TenantId_UserId` (TenantId, UserId)
- `IX_Preferences_TenantId` (TenantId)

**Default Preferences (created on user registration):**
```json
{
  "userId": "user-uuid-123",
  "channel": "*",
  "isEnabled": true,
  "quietHours": {
    "enabled": false,
    "startTime": "22:00",
    "endTime": "08:00",
    "timezone": "America/New_York"
  },
  "topics": ["order_updates", "security_alerts"],
  "locale": "en-US"
}
```

---

### 4.9. Conversations Table (In-App Messaging)

**Purpose:** In-app messaging conversations (Customer↔Support, Customer↔Driver)

| Field            | Type           | Nullable | Description                               |
|------------------|----------------|----------|-------------------------------------------|
| ConversationId   | GUID (PK)      | No       | Conversation ID                           |
| Type             | string         | No       | customer_support, customer_driver         |
| ParticipantsJson | nvarchar(max)  | No       | JSON array of participant user IDs        |
| ServiceOrigin    | string         | No       | orders, ridesharing                       |
| CreatedAt        | datetime       | No       | Conversation creation timestamp           |
| LastMessageAt    | datetime       | Yes      | Timestamp of most recent message          |

**Indexes:**
- `IX_Conversations_LastMessageAt` (LastMessageAt DESC)
- Full-text index on ParticipantsJson for user lookup

**Retention:**
- Customer↔Support: 180 days after last message
- Customer↔Driver: 30 days after last message

---

### 4.10. Messages Table (In-App Messaging)

**Purpose:** Individual messages within conversations

| Field            | Type           | Nullable | Description                               |
|------------------|----------------|----------|-------------------------------------------|
| MessageId        | GUID (PK)      | No       | Message ID                                |
| ConversationId   | GUID (FK)      | No       | Parent conversation                       |
| SenderUserId     | string         | No       | User ID of sender                         |
| RecipientUserId  | string         | Yes      | Specific recipient (null=all participants)|
| Body             | nvarchar(max)  | No       | Message content (text, markdown)          |
| AttachmentsJson  | nvarchar(max)  | Yes      | JSON array of file URLs                   |
| Status           | string         | No       | sent, delivered, read, failed             |
| SentAt           | datetime       | No       | Message sent timestamp                    |
| ReadAt           | datetime       | Yes      | Message read timestamp                    |

**Indexes:**
- `IX_Messages_ConversationId_SentAt` (ConversationId, SentAt DESC)
- `IX_Messages_RecipientUserId_Status` (RecipientUserId, Status) WHERE RecipientUserId IS NOT NULL

**Foreign Key:** ConversationId → Conversations.ConversationId (ON DELETE CASCADE)

---

### 4.11. Devices Table

**Purpose:** User devices for push notification delivery

| Field          | Type           | Nullable | Description                               |
|----------------|----------------|----------|-------------------------------------------|
| DeviceId       | GUID (PK)      | No       | Device ID                                 |
| UserId         | string         | No       | User owning the device                    |
| DeviceToken    | string         | No       | FCM/APNS token (hashed for security)      |
| Platform       | string         | No       | android, ios, web                         |
| DeviceInfo     | nvarchar(max)  | Yes      | JSON: model, osVersion, appVersion        |
| LastSeen       | datetime       | No       | Last active timestamp                     |
| Active         | bool           | No       | Is device active (default: true)          |
| CreatedAt      | datetime       | No       | Device registration timestamp             |
| UpdatedAt      | datetime       | No       | Last update timestamp                     |

**Unique Constraint:** (DeviceToken) to prevent duplicates

**Indexes:**
- `IX_Devices_UserId_Active` (UserId, Active)
- `IX_Devices_LastSeen` (LastSeen) for cleanup

**Cleanup:** Remove devices inactive for > 90 days

---

### 4.11.1. Multi-Tenancy Isolation Strategy

**Implementation Model:** Schema-based isolation with `TenantId` scoping

**Tenant-Scoped Tables:**
- Notifications
- CostTracking
- RateLimiting (tenant-specific overrides, null TenantId for global defaults)
- Preferences

**Global Tables (Not Tenant-Scoped):**
- Templates (shared across all tenants)
- RetryPolicy (global retry configurations)
- AuditLog (tenant tracked via context, not foreign key)
- Devices (user-scoped, tenant inferred from user)
- Conversations/Messages (user-scoped, tenant inferred)

**Data Isolation Rules:**
1. All queries MUST include `WHERE TenantId = @TenantId` for tenant-scoped tables
2. User authentication includes TenantId in JWT claims (`tenant_id` claim)
3. Service-to-service calls include `X-Tenant-Id` header for tenant context
4. Budgets and costs tracked independently per tenant
5. Rate limits can be overridden per tenant (tenant-specific configs take precedence over global)

**Tenant Context Flow:**
```
Client Request → JWT → Extract TenantId from claims → Set in HttpContext
→ Repository layer enforces TenantId filter → Database query scoped by TenantId
```

**Row-Level Security (Optional Enhancement):**
```sql
CREATE SECURITY POLICY NotificationsTenantPolicy
ADD FILTER PREDICATE dbo.fn_TenantAccessPredicate(TenantId)
ON dbo.Notifications
WITH (STATE = ON);
```

---

### 4.12. Entity Relationship Diagram (ERD)

```mermaid
erDiagram
    NOTIFICATIONS ||--o{ NOTIFICATION_QUEUE : "originates_from"
    NOTIFICATION_QUEUE }o--|| RETRY_POLICY : "uses"
    NOTIFICATIONS ||--o{ COST_TRACKING : "tracks_cost"
    TEMPLATES ||--o{ NOTIFICATION_QUEUE : "renders_with"
    PREFERENCES ||--o{ NOTIFICATION_QUEUE : "filters"
    DEVICES ||--o{ NOTIFICATION_QUEUE : "targets"
    CONVERSATIONS ||--o{ MESSAGES : "contains"
    AUDIT_LOG ||--o{ NOTIFICATIONS : "audits"
    AUDIT_LOG ||--o{ TEMPLATES : "audits"
    AUDIT_LOG ||--o{ PREFERENCES : "audits"
    RATE_LIMITING ||--o{ NOTIFICATION_QUEUE : "limits"

    NOTIFICATIONS {
        guid Id PK
        string UserId
        string ServiceOrigin
        string Channel
        string TemplateKey
        string Status
        datetime CreatedAt
    }

    NOTIFICATION_QUEUE {
        guid QueueId PK
        string UserId
        string ServiceOrigin
        string Channel
        varbinary EncryptedEmail
        varbinary EncryptedPhoneNumber
        int Attempts
        datetime NextAttemptAt
    }

    RETRY_POLICY {
        guid PolicyId PK
        string ServiceOrigin
        string Channel
        int MaxAttempts
        int BaseDelaySeconds
    }

    COST_TRACKING {
        guid CostId PK
        string ServiceOrigin
        string Channel
        bigint TotalCostMicros
        datetime OccurredAt
    }

    TEMPLATES {
        guid TemplateId PK
        string TemplateKey
        string Channel
        string Locale
        string Body
    }

    PREFERENCES {
        guid PreferenceId PK
        string UserId
        string Channel
        bool IsEnabled
        json QuietHours
    }

    CONVERSATIONS {
        guid ConversationId PK
        string Type
        json ParticipantsJson
        datetime LastMessageAt
    }

    MESSAGES {
        guid MessageId PK
        guid ConversationId FK
        string SenderUserId
        string Body
        datetime SentAt
    }

    DEVICES {
        guid DeviceId PK
        string UserId
        string DeviceToken
        string Platform
        bool Active
    }

    AUDIT_LOG {
        guid AuditId PK
        string Action
        string EntityType
        guid EntityId
        datetime OccurredAt
    }

    RATE_LIMITING {
        guid ConfigId PK
        string ServiceOrigin
        string Channel
        int PerUserMax
        int PerServiceMax
    }
```

---

### 4.13. Migration Strategy

**Phase 1: Core Tables**
1. Notifications, NotificationQueue, Templates, Preferences, Devices
2. Seed default RetryPolicy and RateLimiting configurations

**Phase 2: Supporting Tables**
3. CostTracking, AuditLog
4. Create monthly rollup views

**Phase 3: Messaging Tables**
5. Conversations, Messages
6. Add full-text indexes

**Phase 4: Optimization**
7. Add composite indexes based on query patterns
8. Partition AuditLog by OccurredAt (monthly partitions)
9. Create archived tables for retention compliance

**EF Core Migrations:**
```bash
dotnet ef migrations add InitialCreate --project src/Listo.Notification.Infrastructure
dotnet ef database update --project src/Listo.Notification.Infrastructure
```

**Rollback Strategy:**
- All migrations reversible with DOWN scripts
- Database backups before each migration
- Blue-green deployment for zero-downtime migrations

---

## 5. Authentication & Authorization

### 5.1. Client Authentication (User Requests)

**Method:** JWT Bearer Token Authentication

**Token Issuer:** Listo.Auth service

**Configuration:**
```csharp
// Program.cs or Startup.cs
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Auth:Authority"]; // Listo.Auth URL
        options.Audience = "listo-notification-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });
```

**JWT Claims Expected:**
- `sub`: User ID (maps to UserId in our tables)
- `role`: User roles (Customer, Driver, Support, Admin)
- `permissions`: Granular permissions array
- `email`: User email (for audit logging)
- `name`: User display name

**Usage:**
```csharp
[Authorize] // Requires any authenticated user
[Authorize(Roles = "Admin")] // Requires Admin role
[Authorize(Policy = "ManageTemplates")] // Requires specific policy
```

---

### 5.2. Service-to-Service Authentication

**Method:** Shared Secret Header (`X-Service-Secret`)

**Secret Storage:** Azure Key Vault

**Secret Names (per service):**
- `listo-auth-service-secret`
- `listo-orders-service-secret`
- `listo-ridesharing-service-secret`
- `listo-products-service-secret`

**Implementation:**
```csharp
// Middleware: ServiceSecretAuthenticationMiddleware.cs
public class ServiceSecretAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, string> _serviceSecrets;

    public ServiceSecretAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IKeyVaultService keyVaultService)
    {
        _next = next;
        _configuration = configuration;
        
        // Load secrets from Key Vault on startup
        _serviceSecrets = new Dictionary<string, string>
        {
            ["auth"] = keyVaultService.GetSecret("listo-auth-service-secret"),
            ["orders"] = keyVaultService.GetSecret("listo-orders-service-secret"),
            ["ridesharing"] = keyVaultService.GetSecret("listo-ridesharing-service-secret"),
            ["products"] = keyVaultService.GetSecret("listo-products-service-secret")
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only check secret for internal endpoints
        if (context.Request.Path.StartsWithSegments("/api/v1/internal"))
        {
            if (!context.Request.Headers.TryGetValue("X-Service-Secret", out var secretHeader))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "MISSING_SERVICE_SECRET",
                        message = "X-Service-Secret header is required for internal endpoints"
                    }
                });
                return;
            }

            var providedSecret = secretHeader.ToString();
            var isValid = _serviceSecrets.Values.Any(secret => 
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(secret),
                    Encoding.UTF8.GetBytes(providedSecret)
                ));

            if (!isValid)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        code = "INVALID_SERVICE_SECRET",
                        message = "Invalid service secret provided"
                    }
                });
                return;
            }

            // Add service origin to context for auditing
            var serviceOrigin = _serviceSecrets.FirstOrDefault(x => 
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(x.Value),
                    Encoding.UTF8.GetBytes(providedSecret)
                )).Key;
            
            context.Items["ServiceOrigin"] = serviceOrigin;
        }

        await _next(context);
    }
}
```

**Secret Rotation Strategy:**
1. Generate new secret in Key Vault with versioning
2. Update calling services to use new secret
3. Configure grace period (7 days) where both old and new secrets are valid
4. After grace period, remove old secret from validation
5. Automated rotation every 90 days via Azure Function

---

### 5.3. Required Headers

**For Client Requests:**
```http
Authorization: Bearer {jwt_token}
Content-Type: application/json
X-Correlation-Id: {uuid}  # Required for tracing
X-Idempotency-Key: {unique_key}  # Required for POST operations
```

**For Service-to-Service Requests:**
```http
X-Service-Secret: {shared_secret}
Content-Type: application/json
X-Correlation-Id: {uuid}  # Required for tracing
X-Idempotency-Key: {unique_key}  # Required for POST operations
traceparent: {w3c_trace_context}  # OpenTelemetry tracing
```

---

### 5.4. Role-Based Authorization

**Roles Defined by Listo.Auth:**
- **Customer:** Can view own notifications and preferences
- **Driver:** Can view own notifications and preferences
- **Support:** Can view all notifications, manage support conversations
- **Admin:** Full access to all features, templates, analytics

**Authorization Policies:**
```csharp
// Program.cs
services.AddAuthorization(options =>
{
    // Customer policies
    options.AddPolicy("ViewOwnNotifications", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context => 
                  context.User.HasClaim("sub", context.Resource.ToString())));

    // Admin policies
    options.AddPolicy("ManageTemplates", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("ViewAnalytics", policy =>
        policy.RequireRole("Admin", "Support"));

    options.AddPolicy("ManageCostBudgets", policy =>
        policy.RequireRole("Admin"));

    // Support policies
    options.AddPolicy("AccessSupportConversations", policy =>
        policy.RequireRole("Support", "Admin"));
});
```

**Controller Usage:**
```csharp
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ViewOwnNotifications")]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userId = User.FindFirst("sub")?.Value;
        // ...
    }

    [HttpPost("templates")]
    [Authorize(Policy = "ManageTemplates")]
    public async Task<IActionResult> CreateTemplate([FromBody] TemplateDto template)
    {
        // ...
    }
}
```

---

### 5.5. HTTPS & Transport Security

**Enforcement:**
```csharp
// Program.cs
app.UseHttpsRedirection();
app.UseHsts(); // HTTP Strict Transport Security

// Add HSTS configuration
services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});
```

**Certificate Management:**
- Use Azure App Service managed certificates
- Automatic renewal enabled
- TLS 1.2 minimum (TLS 1.3 preferred)

---

### 5.6. Input Validation & Request Limits

**Request Size Limits:**
```csharp
// Program.cs
services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB max
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32 KB
    options.Limits.MaxRequestHeaderCount = 100;
});

services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB for file uploads
});
```

**Input Validation:**
- Use FluentValidation for request DTOs
- Validate all user inputs before processing
- Sanitize data for XSS prevention
- Use parameterized queries (EF Core handles this)

**Example Validator:**
```csharp
public class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .Must(BeValidGuid).WithMessage("UserId must be a valid GUID");

        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(x => new[] { "push", "sms", "email", "inApp" }.Contains(x))
            .WithMessage("Channel must be push, sms, email, or inApp");

        RuleFor(x => x.TemplateKey)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-z0-9_]+$").WithMessage("TemplateKey must be lowercase alphanumeric with underscores");
    }

    private bool BeValidGuid(string value) => Guid.TryParse(value, out _);
}
```

---

### 5.7. CORS Configuration

**Policy:**
```csharp
// Program.cs
services.AddCors(options =>
{
    options.AddPolicy("ListoFrontends", builder =>
    {
        builder
            .WithOrigins(
                configuration["Cors:AllowedOrigins"].Split(",") // From appsettings
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Correlation-Id", "X-RateLimit-Remaining")
            .AllowCredentials(); // For SignalR
    });
});

app.UseCors("ListoFrontends");
```

**appsettings.json:**
```json
{
  "Cors": {
    "AllowedOrigins": "https://app.listoexpress.com,https://admin.listoexpress.com,https://driver.listoexpress.com"
  }
}
```

---

### 5.8. Security Headers

**Middleware:**
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Content-Security-Policy", 
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'");
    
    await next();
});
```

---

### 5.9. Authentication Flow Examples

**Client Request Flow:**
```
1. Client → Listo.Auth: POST /auth/login
2. Listo.Auth → Client: {accessToken, refreshToken}
3. Client → Listo.Notification: GET /api/v1/notifications
   Headers: Authorization: Bearer {accessToken}
4. Listo.Notification validates JWT with Listo.Auth's public key
5. Listo.Notification → Client: {notifications}
```

**Service-to-Service Flow:**
```
1. Listo.Orders → Azure Key Vault: Get listo-orders-service-secret
2. Listo.Orders → Listo.Notification: POST /api/v1/internal/notifications/queue
   Headers: X-Service-Secret: {secret}, X-Correlation-Id: {uuid}
3. Listo.Notification validates secret against Key Vault secrets
4. Listo.Notification → Listo.Orders: 200 OK {notificationId}
```

---

### 5.10. Security Checklist

- [ ] JWT validation configured with correct issuer and audience
- [ ] Service secret middleware validates all `/internal` endpoints
- [ ] Secrets stored in Azure Key Vault (never in code or config files)
- [ ] Secret rotation policy implemented (90-day cycle)
- [ ] HTTPS enforced with HSTS enabled
- [ ] CORS configured with explicit allowed origins
- [ ] Request size limits enforced (10 MB max)
- [ ] Input validation with FluentValidation on all DTOs
- [ ] Security headers added (XSS, Frame Options, CSP)
- [ ] Rate limiting implemented (see Section 7)
- [ ] Correlation IDs required and propagated
- [ ] Audit logging for all sensitive operations

---

## 6. Service-Specific Event Mappings

This section defines the canonical events published by each Listo service and how they map to notification templates and delivery channels.

### 6.1. Listo.Auth Service Events

#### 6.1.1. EmailVerificationRequested

**Channels:** Email  
**Template Key:** `email_verification_requested`  
**Priority:** High  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-auth-001",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "EmailVerificationRequested",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-abc-123",
  "idempotencyKey": "auth-email-verify-user-uuid-123",
  "priority": "high",
  "channels": ["email"],
  "templateKey": "email_verification_requested",
  "data": {
    "email": "user@example.com",
    "verificationCode": "ABC123XYZ",
    "verificationLink": "https://app.listoexpress.com/verify?token=xyz",
    "expiryMinutes": 60,
    "userName": "John Doe"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

**Template Variables:** `email`, `verificationCode`, `verificationLink`, `expiryMinutes`, `userName`

**Required Headers:**
- `X-Service-Secret`
- `X-Correlation-Id`
- `X-Idempotency-Key`

---

#### 6.1.2. MobileVerificationRequested

**Channels:** SMS  
**Template Key:** `mobile_verification_requested`  
**Priority:** High  
**Delivery:** Asynchronous  
**Timeout:** 20 seconds

**Event Payload:**
```json
{
  "eventId": "evt-auth-002",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "MobileVerificationRequested",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-abc-124",
  "idempotencyKey": "auth-mobile-verify-user-uuid-123",
  "priority": "high",
  "channels": ["sms"],
  "templateKey": "mobile_verification_requested",
  "data": {
    "phoneNumber": "+1234567890",
    "otpCode": "123456",
    "expiryMinutes": 10,
    "appName": "ListoExpress"
  }
}
```

**Template Variables:** `phoneNumber`, `otpCode`, `expiryMinutes`, `appName`

---

#### 6.1.3. PasswordResetRequested

**Channels:** Email, SMS  
**Template Key:** `password_reset_requested`  
**Priority:** High  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-auth-003",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "PasswordResetRequested",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-abc-125",
  "idempotencyKey": "auth-password-reset-user-uuid-123",
  "priority": "high",
  "channels": ["email", "sms"],
  "templateKey": "password_reset_requested",
  "data": {
    "email": "user@example.com",
    "phoneNumber": "+1234567890",
    "resetCode": "789456",
    "resetLink": "https://app.listoexpress.com/reset-password?token=xyz",
    "expiryMinutes": 15,
    "userName": "John Doe",
    "ipAddress": "192.168.1.1"
  }
}
```

**Template Variables:** `email`, `phoneNumber`, `resetCode`, `resetLink`, `expiryMinutes`, `userName`, `ipAddress`

---

#### 6.1.4. TwoFactorCodeIssued

**Channels:** SMS, Email  
**Template Key:** `two_factor_code_issued`  
**Priority:** High  
**Delivery:** Asynchronous  
**Timeout:** 20 seconds

**Event Payload:**
```json
{
  "eventId": "evt-auth-004",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "TwoFactorCodeIssued",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-abc-126",
  "idempotencyKey": "auth-2fa-code-user-uuid-123",
  "priority": "high",
  "channels": ["sms", "email"],
  "templateKey": "two_factor_code_issued",
  "data": {
    "twoFactorCode": "654321",
    "expiryMinutes": 5,
    "deviceInfo": "Chrome on Windows",
    "ipAddress": "192.168.1.1"
  }
}
```

**Template Variables:** `twoFactorCode`, `expiryMinutes`, `deviceInfo`, `ipAddress`

---

#### 6.1.5. SuspiciousLoginDetected

**Channels:** Email, Push  
**Template Key:** `suspicious_login_detected`  
**Priority:** High  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-auth-005",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "SuspiciousLoginDetected",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-abc-127",
  "idempotencyKey": "auth-suspicious-login-user-uuid-123-1705315800",
  "priority": "high",
  "channels": ["email", "push"],
  "templateKey": "suspicious_login_detected",
  "data": {
    "userName": "John Doe",
    "loginTime": "2024-01-15T10:30:00Z",
    "deviceInfo": "Unknown Device - Chrome on Linux",
    "ipAddress": "203.0.113.42",
    "location": "Unknown Location",
    "actionLink": "https://app.listoexpress.com/security/review"
  }
}
```

**Template Variables:** `userName`, `loginTime`, `deviceInfo`, `ipAddress`, `location`, `actionLink`

---

### 6.2. Listo.Orders Service Events

#### 6.2.1. OrderConfirmed

**Channels:** Email, Push  
**Template Key:** `order_confirmed`  
**Priority:** Normal  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-orders-001",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "OrderConfirmed",
  "serviceOrigin": "orders",
  "userId": "customer-uuid-456",
  "correlationId": "trace-order-001",
  "idempotencyKey": "orders-confirmed-ORD-001",
  "priority": "normal",
  "channels": ["email", "push"],
  "templateKey": "order_confirmed",
  "data": {
    "orderId": "ORD-001",
    "orderNumber": "ORD-20240115-001",
    "customerName": "Jane Smith",
    "merchantName": "Pizza Palace",
    "totalAmount": "$28.98",
    "estimatedDeliveryTime": "6:30 PM",
    "deliveryAddress": "123 Main St, Apt 4B",
    "orderDetailsLink": "https://app.listoexpress.com/orders/ORD-001"
  }
}
```

**Template Variables:** `orderId`, `orderNumber`, `customerName`, `merchantName`, `totalAmount`, `estimatedDeliveryTime`, `deliveryAddress`, `orderDetailsLink`

---

#### 6.2.2. OrderStatusUpdated

**Channels:** Push, SMS (optional)  
**Template Key:** `order_status_updated`  
**Priority:** Normal  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-orders-002",
  "occurredAt": "2024-01-15T10:45:00Z",
  "messageType": "OrderStatusUpdated",
  "serviceOrigin": "orders",
  "userId": "customer-uuid-456",
  "correlationId": "trace-order-001",
  "idempotencyKey": "orders-status-ORD-001-preparing",
  "priority": "normal",
  "channels": ["push"],
  "templateKey": "order_status_updated",
  "data": {
    "orderId": "ORD-001",
    "orderNumber": "ORD-20240115-001",
    "previousStatus": "accepted",
    "newStatus": "preparing",
    "statusMessage": "Your order is being prepared",
    "estimatedDeliveryTime": "6:30 PM"
  }
}
```

**Template Variables:** `orderId`, `orderNumber`, `previousStatus`, `newStatus`, `statusMessage`, `estimatedDeliveryTime`

---

#### 6.2.3. DriverAssigned (SYNCHRONOUS)

**Channels:** Push  
**Template Key:** `driver_assigned`  
**Priority:** High  
**Delivery:** **SYNCHRONOUS** (< 2 seconds)  
**Timeout:** 15 seconds

**⚠️ CRITICAL PATH:** This event MUST be delivered synchronously via direct REST API call with `synchronous: true`

**Event Payload:**
```json
{
  "eventId": "evt-orders-003",
  "occurredAt": "2024-01-15T10:50:00Z",
  "messageType": "DriverAssigned",
  "serviceOrigin": "orders",
  "userId": "driver-uuid-789",
  "correlationId": "trace-order-001",
  "idempotencyKey": "orders-driver-assigned-ORD-001-driver-789",
  "priority": "high",
  "channels": ["push"],
  "templateKey": "driver_assigned",
  "synchronous": true,
  "data": {
    "orderId": "ORD-001",
    "orderNumber": "ORD-20240115-001",
    "customerName": "Jane Smith",
    "pickupAddress": "Pizza Palace, 456 Business Ave",
    "deliveryAddress": "123 Main St, Apt 4B",
    "estimatedPickupTime": "6:15 PM",
    "estimatedDeliveryTime": "6:30 PM",
    "orderTotal": "$28.98",
    "deliveryFee": "$5.99",
    "orderDetailsLink": "listo://orders/ORD-001"
  }
}
```

**Template Variables:** `orderId`, `orderNumber`, `customerName`, `pickupAddress`, `deliveryAddress`, `estimatedPickupTime`, `estimatedDeliveryTime`, `orderTotal`, `deliveryFee`, `orderDetailsLink`

**Implementation Note:** Must use `POST /api/v1/internal/notifications/queue` with `synchronous: true` flag. API will send FCM push + SignalR broadcast in parallel before returning.

---

#### 6.2.4. DeliveryCompleted

**Channels:** Email, Push  
**Template Key:** `delivery_completed`  
**Priority:** Normal  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-orders-004",
  "occurredAt": "2024-01-15T18:28:00Z",
  "messageType": "DeliveryCompleted",
  "serviceOrigin": "orders",
  "userId": "customer-uuid-456",
  "correlationId": "trace-order-001",
  "idempotencyKey": "orders-delivered-ORD-001",
  "priority": "normal",
  "channels": ["email", "push"],
  "templateKey": "delivery_completed",
  "data": {
    "orderId": "ORD-001",
    "orderNumber": "ORD-20240115-001",
    "deliveredAt": "6:28 PM",
    "driverName": "Alex Driver",
    "ratingLink": "https://app.listoexpress.com/orders/ORD-001/rate",
    "receiptLink": "https://app.listoexpress.com/orders/ORD-001/receipt"
  }
}
```

**Template Variables:** `orderId`, `orderNumber`, `deliveredAt`, `driverName`, `ratingLink`, `receiptLink`

---

### 6.3. Listo.RideSharing Service Events

#### 6.3.1. RideBooked

**Channels:** Push, Email  
**Template Key:** `ride_booked`  
**Priority:** Normal  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-ridesharing-001",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "RideBooked",
  "serviceOrigin": "ridesharing",
  "userId": "passenger-uuid-111",
  "correlationId": "trace-ride-001",
  "idempotencyKey": "ridesharing-booked-RIDE-001",
  "priority": "normal",
  "channels": ["push", "email"],
  "templateKey": "ride_booked",
  "data": {
    "rideId": "RIDE-001",
    "passengerName": "Bob Passenger",
    "rideType": "Economy",
    "pickupAddress": "123 Main St",
    "dropoffAddress": "456 Pine St",
    "scheduledTime": "3:00 PM",
    "estimatedFare": "$12.50",
    "rideDetailsLink": "https://app.listoexpress.com/rides/RIDE-001"
  }
}
```

**Template Variables:** `rideId`, `passengerName`, `rideType`, `pickupAddress`, `dropoffAddress`, `scheduledTime`, `estimatedFare`, `rideDetailsLink`

---

#### 6.3.2. DriverAssigned (SYNCHRONOUS)

**Channels:** Push  
**Template Key:** `ridesharing_driver_assigned`  
**Priority:** High  
**Delivery:** **SYNCHRONOUS** (< 2 seconds)  
**Timeout:** 15 seconds

**⚠️ CRITICAL PATH:** Must be delivered synchronously to both driver and passenger

**Event Payload (to Driver):**
```json
{
  "eventId": "evt-ridesharing-002",
  "occurredAt": "2024-01-15T10:32:00Z",
  "messageType": "DriverAssigned",
  "serviceOrigin": "ridesharing",
  "userId": "driver-uuid-222",
  "correlationId": "trace-ride-001",
  "idempotencyKey": "ridesharing-driver-assigned-RIDE-001-driver-222",
  "priority": "high",
  "channels": ["push"],
  "templateKey": "ridesharing_driver_assigned",
  "synchronous": true,
  "data": {
    "rideId": "RIDE-001",
    "passengerName": "Bob Passenger",
    "passengerRating": "4.8",
    "pickupAddress": "123 Main St",
    "dropoffAddress": "456 Pine St",
    "estimatedPickupTime": "2:45 PM",
    "estimatedFare": "$12.50",
    "rideDetailsLink": "listo://rides/RIDE-001"
  }
}
```

**Event Payload (to Passenger):**
```json
{
  "eventId": "evt-ridesharing-003",
  "occurredAt": "2024-01-15T10:32:00Z",
  "messageType": "DriverAssignedToPassenger",
  "serviceOrigin": "ridesharing",
  "userId": "passenger-uuid-111",
  "correlationId": "trace-ride-001",
  "idempotencyKey": "ridesharing-driver-assigned-passenger-RIDE-001",
  "priority": "high",
  "channels": ["push"],
  "templateKey": "driver_assigned_to_passenger",
  "synchronous": false,
  "data": {
    "rideId": "RIDE-001",
    "driverName": "Alex Driver",
    "driverRating": "4.9",
    "vehicleType": "Toyota Prius",
    "vehicleColor": "Silver",
    "licensePlate": "ABC123",
    "estimatedArrival": "5 minutes",
    "driverPhotoUrl": "https://cdn.listoexpress.com/drivers/222.jpg"
  }
}
```

**Template Variables (Driver):** `rideId`, `passengerName`, `passengerRating`, `pickupAddress`, `dropoffAddress`, `estimatedPickupTime`, `estimatedFare`, `rideDetailsLink`

**Template Variables (Passenger):** `rideId`, `driverName`, `driverRating`, `vehicleType`, `vehicleColor`, `licensePlate`, `estimatedArrival`, `driverPhotoUrl`

---

#### 6.3.3. DriverArriving

**Channels:** Push  
**Template Key:** `driver_arriving`  
**Priority:** High  
**Delivery:** Asynchronous  
**Timeout:** 20 seconds

**Event Payload:**
```json
{
  "eventId": "evt-ridesharing-004",
  "occurredAt": "2024-01-15T10:40:00Z",
  "messageType": "DriverArriving",
  "serviceOrigin": "ridesharing",
  "userId": "passenger-uuid-111",
  "correlationId": "trace-ride-001",
  "idempotencyKey": "ridesharing-arriving-RIDE-001",
  "priority": "high",
  "channels": ["push"],
  "templateKey": "driver_arriving",
  "data": {
    "rideId": "RIDE-001",
    "driverName": "Alex Driver",
    "estimatedArrival": "2 minutes",
    "vehicleType": "Toyota Prius",
    "licensePlate": "ABC123"
  }
}
```

**Template Variables:** `rideId`, `driverName`, `estimatedArrival`, `vehicleType`, `licensePlate`

---

#### 6.3.4. RideCompleted

**Channels:** Email, Push  
**Template Key:** `ride_completed`  
**Priority:** Normal  
**Delivery:** Asynchronous  
**Timeout:** 30 seconds

**Event Payload:**
```json
{
  "eventId": "evt-ridesharing-005",
  "occurredAt": "2024-01-15T11:15:00Z",
  "messageType": "RideCompleted",
  "serviceOrigin": "ridesharing",
  "userId": "passenger-uuid-111",
  "correlationId": "trace-ride-001",
  "idempotencyKey": "ridesharing-completed-RIDE-001",
  "priority": "normal",
  "channels": ["email", "push"],
  "templateKey": "ride_completed",
  "data": {
    "rideId": "RIDE-001",
    "completedAt": "11:15 AM",
    "driverName": "Alex Driver",
    "finalFare": "$14.00",
    "distance": "5.2 miles",
    "duration": "15 minutes",
    "ratingLink": "https://app.listoexpress.com/rides/RIDE-001/rate",
    "receiptLink": "https://app.listoexpress.com/rides/RIDE-001/receipt"
  }
}
```

**Template Variables:** `rideId`, `completedAt`, `driverName`, `finalFare`, `distance`, `duration`, `ratingLink`, `receiptLink`

---

### 6.4. Event Publishing Guidelines

#### Service Bus Topic Publishing

**Topic:** `listo-notifications-events`

**Application Properties:**
```json
{
  "ServiceOrigin": "orders",
  "MessageType": "OrderConfirmed",
  "Priority": "normal",
  "CorrelationId": "trace-order-001",
  "ContentType": "application/json"
}
```

**Subscription Filters:**
- `auth-notifications`: `ServiceOrigin = 'auth'`
- `orders-notifications`: `ServiceOrigin = 'orders'`
- `ridesharing-notifications`: `ServiceOrigin = 'ridesharing'`

#### Direct REST API (Synchronous Only)

**Endpoint:** `POST /api/v1/internal/notifications/queue`

**When to Use:**
- Driver assignment alerts (orders and ridesharing)
- Any notification requiring sub-2-second delivery confirmation

**Example Request:**
```http
POST /api/v1/internal/notifications/queue
Host: notification-api.listoexpress.com
X-Service-Secret: {secret}
X-Correlation-Id: trace-order-001
X-Idempotency-Key: orders-driver-assigned-ORD-001
Content-Type: application/json

{
  "userId": "driver-uuid-789",
  "serviceOrigin": "orders",
  "channel": "push",
  "templateKey": "driver_assigned",
  "priority": "high",
  "synchronous": true,
  "data": { ... },
  "encryptedFirebaseToken": "encrypted_token_here"
}
```

**Response (Success):**
```json
{
  "notificationId": "notif-uuid-999",
  "status": "delivered",
  "channels": {
    "push": {
      "delivered": true,
      "providerMessageId": "fcm-msg-123",
      "deliveredAt": "2024-01-15T10:50:01.234Z"
    },
    "signalr": {
      "broadcast": true,
      "deliveredAt": "2024-01-15T10:50:01.156Z"
    }
  },
  "processingTimeMs": 1234
}
```

---

### 6.5. Event Mapping Summary Table

| Service | Event | Channels | Priority | Sync/Async | Template Key |
|---------|-------|----------|----------|------------|-------------|
| **Auth** | EmailVerificationRequested | Email | High | Async | `email_verification_requested` |
| **Auth** | MobileVerificationRequested | SMS | High | Async | `mobile_verification_requested` |
| **Auth** | PasswordResetRequested | Email, SMS | High | Async | `password_reset_requested` |
| **Auth** | TwoFactorCodeIssued | SMS, Email | High | Async | `two_factor_code_issued` |
| **Auth** | SuspiciousLoginDetected | Email, Push | High | Async | `suspicious_login_detected` |
| **Orders** | OrderConfirmed | Email, Push | Normal | Async | `order_confirmed` |
| **Orders** | OrderStatusUpdated | Push | Normal | Async | `order_status_updated` |
| **Orders** | **DriverAssigned** | **Push** | **High** | **Sync** | `driver_assigned` |
| **Orders** | DeliveryCompleted | Email, Push | Normal | Async | `delivery_completed` |
| **RideSharing** | RideBooked | Push, Email | Normal | Async | `ride_booked` |
| **RideSharing** | **DriverAssigned** | **Push** | **High** | **Sync** | `ridesharing_driver_assigned` |
| **RideSharing** | DriverArriving | Push | High | Async | `driver_arriving` |
| **RideSharing** | RideCompleted | Email, Push | Normal | Async | `ride_completed` |

**Total Events:** 13 (2 synchronous, 11 asynchronous)

---

## 7. Cost Management & Rate Limiting

This section defines the cost tracking, budget management, and rate limiting strategies for the notification service. Both per-tenant and per-service tracking are implemented with maximum caps enforcement.

### 7.1. Redis Token Bucket Rate Limiting

**Implementation:** Redis-based token bucket algorithm with atomic operations

**Key Structure:**
- **Per-User:** `rl:user:{tenantId}:{userId}:{channel}`
- **Per-Service:** `rl:service:{tenantId}:{serviceOrigin}:{channel}`
- **Per-Tenant:** `rl:tenant:{tenantId}:{channel}`

**Token Bucket Data:**
```json
{
  "tokens": 60,
  "capacity": 60,
  "refillRate": 60,
  "refillIntervalSeconds": 3600,
  "lastRefillTimestamp": 1705315800
}
```

**Lua Script (Atomic Check and Consume):**
```lua
-- rate_limit.lua
local key = KEYS[1]
local capacity = tonumber(ARGV[1])
local refillRate = tonumber(ARGV[2])
local refillInterval = tonumber(ARGV[3])
local requestedTokens = tonumber(ARGV[4])
local now = tonumber(ARGV[5])

-- Get current bucket state
local bucket = redis.call('HMGET', key, 'tokens', 'lastRefillTimestamp')
local tokens = tonumber(bucket[1]) or capacity
local lastRefill = tonumber(bucket[2]) or now

-- Calculate tokens to add based on time elapsed
local elapsedSeconds = now - lastRefill
if elapsedSeconds > 0 then
    local tokensToAdd = math.floor((elapsedSeconds / refillInterval) * refillRate)
    tokens = math.min(capacity, tokens + tokensToAdd)
    lastRefill = now
end

-- Check if enough tokens available
if tokens >= requestedTokens then
    tokens = tokens - requestedTokens
    redis.call('HMSET', key, 'tokens', tokens, 'lastRefillTimestamp', lastRefill)
    redis.call('EXPIRE', key, refillInterval * 2)
    return {1, tokens, capacity}
else
    redis.call('HMSET', key, 'tokens', tokens, 'lastRefillTimestamp', lastRefill)
    redis.call('EXPIRE', key, refillInterval * 2)
    local retryAfter = math.ceil((requestedTokens - tokens) * refillInterval / refillRate)
    return {0, tokens, retryAfter}
end
```

**C# .NET 9 Implementation:**
```csharp
using StackExchange.Redis;
using System.Security.Cryptography;

namespace Listo.Notification.Infrastructure.RateLimiting;

public class RedisTokenBucketLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _luaScript;
    
    public RedisTokenBucketLimiter(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _luaScript = LoadLuaScript(); // Load from embedded resource or file
    }
    
    public async Task<RateLimitResult> CheckAndConsumeAsync(
        string tenantId,
        string userId,
        string channel,
        RateLimitConfig config,
        int tokensRequested = 1)
    {
        var key = $"rl:user:{tenantId}:{userId}:{channel}";
        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var result = await db.ScriptEvaluateAsync(
            _luaScript,
            new RedisKey[] { key },
            new RedisValue[] 
            {
                config.Capacity,
                config.RefillRate,
                config.RefillIntervalSeconds,
                tokensRequested,
                now
            }
        );
        
        var values = (RedisValue[])result!;
        var allowed = (int)values[0] == 1;
        var remainingTokens = (int)values[1];
        var retryAfterOrCapacity = (int)values[2];
        
        return new RateLimitResult
        {
            Allowed = allowed,
            Limit = config.Capacity,
            Remaining = remainingTokens,
            RetryAfterSeconds = allowed ? null : retryAfterOrCapacity,
            ResetAt = DateTimeOffset.UtcNow.AddSeconds(config.RefillIntervalSeconds)
        };
    }
    
    public async Task<RateLimitResult> CheckServiceLimitAsync(
        string tenantId,
        string serviceOrigin,
        string channel,
        RateLimitConfig config)
    {
        var key = $"rl:service:{tenantId}:{serviceOrigin}:{channel}";
        // Similar implementation
        return await CheckAndConsumeInternalAsync(key, config);
    }
    
    private async Task<RateLimitResult> CheckAndConsumeInternalAsync(
        string key, 
        RateLimitConfig config, 
        int tokensRequested = 1)
    {
        // Implementation details...
    }
}

public record RateLimitConfig(
    int Capacity,
    int RefillRate,
    int RefillIntervalSeconds,
    int MaxCap
);

public record RateLimitResult
{
    public required bool Allowed { get; init; }
    public required int Limit { get; init; }
    public required int Remaining { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public DateTimeOffset ResetAt { get; init; }
}
```

---

### 7.2. Rate Limiting Middleware

**HTTP Response Headers:**
- `X-RateLimit-Limit`: Maximum requests allowed in window
- `X-RateLimit-Remaining`: Remaining requests in current window
- `X-RateLimit-Reset`: Unix timestamp when window resets
- `Retry-After`: Seconds to wait before retrying (429 responses only)

**Middleware Implementation:**
```csharp
using Microsoft.AspNetCore.Http;

namespace Listo.Notification.API.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RedisTokenBucketLimiter _limiter;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    
    public RateLimitingMiddleware(
        RequestDelegate next,
        RedisTokenBucketLimiter limiter,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _limiter = limiter;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Extract tenant and user from JWT claims
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        var userId = context.User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }
        
        // Get rate limit config from database (cached)
        var config = await GetRateLimitConfigAsync(tenantId, "*", "api");
        
        // Check rate limit
        var result = await _limiter.CheckAndConsumeAsync(
            tenantId, userId, "api", config
        );
        
        // Add headers
        context.Response.Headers.Append("X-RateLimit-Limit", result.Limit.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining", result.Remaining.ToString());
        context.Response.Headers.Append("X-RateLimit-Reset", result.ResetAt.ToUnixTimeSeconds().ToString());
        
        if (!result.Allowed)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers.Append("Retry-After", result.RetryAfterSeconds.ToString());
            
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "RATE_LIMIT_EXCEEDED",
                    message = "Rate limit exceeded. Please retry after the specified time.",
                    retryAfter = result.RetryAfterSeconds,
                    limit = result.Limit
                }
            });
            
            _logger.LogWarning(
                "Rate limit exceeded for tenant {TenantId}, user {UserId}",
                tenantId, userId
            );
            return;
        }
        
        await _next(context);
    }
    
    private async Task<RateLimitConfig> GetRateLimitConfigAsync(
        string tenantId, string serviceOrigin, string channel)
    {
        // Query RateLimiting table with tenant-specific fallback
        // Tenant-specific config overrides global default
        return new RateLimitConfig(60, 60, 3600, 100); // Example
    }
}
```

---

### 7.3. Quota Enforcement Strategy

**Priority Order (Admin Override → Service Quota → User Quota → Global):**

1. **Admin Override Check:** If `X-Admin-Override: true` header present with valid `notifications:admin` scope
2. **Service Quota Check:** Per-service daily/monthly caps from `RateLimiting` table
3. **User Quota Check:** Per-user hourly caps from `RateLimiting` table  
4. **Maximum Cap Enforcement:** Absolute maximum (cannot be overridden except by admin)

**Quota Check Flow:**
```mermaid
flowchart TD
    A[Incoming Request] --> B{Admin Override Header?}
    B -->|Yes| C{Valid Admin Token?}
    C -->|Yes| D[Allow - Log Override]
    C -->|No| E[Deny - Invalid Admin]
    B -->|No| F{Check Tenant Service Quota}
    F -->|Within Limit| G{Check User Quota}
    F -->|Exceeded| H{Below Max Cap?}
    H -->|Yes| I[Allow - Warn Near Limit]
    H -->|No| J[Deny - 429 Max Cap]
    G -->|Within Limit| K[Allow - Add Headers]
    G -->|Exceeded| L[Deny - 429 User Quota]
    K --> M[Process Request]
    D --> M
    I --> M
```

---

### 7.4. Budget Tracking (Per-Tenant AND Per-Service)

**Budget Configuration:**
```json
{
  "tenantId": "tenant-uuid-123",
  "serviceOrigin": "orders",
  "channel": "sms",
  "currency": "USD",
  "monthlyBudgetMicros": 50000000,
  "alertThresholds": [0.8, 1.0],
  "currentSpendMicros": 35000000,
  "periodStart": "2024-01-01T00:00:00Z",
  "periodEnd": "2024-01-31T23:59:59Z"
}
```

**Cost Computation Per Channel:**
- **Email (SendGrid):** $0.00095/email → 950 micros
- **SMS (Twilio US):** $0.0079/message → 7900 micros
- **SMS Multi-segment:** 7900 micros × segment count
- **Push (FCM):** $0 (free, tracked for analytics)
- **In-App:** $0 (free, tracked for analytics)

**Budget Monitoring (Azure Function - Hourly):**
```csharp
using Microsoft.Azure.Functions.Worker;

namespace Listo.Notification.Functions;

public class BudgetMonitorFunction
{
    private readonly NotificationDbContext _context;
    private readonly IServiceBusPublisher _serviceBus;
    
    [Function("BudgetMonitor")]
    public async Task RunAsync(
        [TimerTrigger("0 0 * * * *")] TimerInfo timer) // Every hour
    {
        var currentMonth = DateTime.UtcNow;
        var periodStart = new DateTime(currentMonth.Year, currentMonth.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddSeconds(-1);
        
        // Query cost summaries per tenant and service
        var tenantCosts = await _context.CostTracking
            .Where(c => c.OccurredAt >= periodStart && c.OccurredAt <= periodEnd)
            .GroupBy(c => new { c.TenantId, c.ServiceOrigin, c.Channel, c.Currency })
            .Select(g => new
            {
                g.Key.TenantId,
                g.Key.ServiceOrigin,
                g.Key.Channel,
                g.Key.Currency,
                TotalCostMicros = g.Sum(c => c.TotalCostMicros)
            })
            .ToListAsync();
        
        // Check against budgets
        foreach (var cost in tenantCosts)
        {
            var budget = await GetBudgetAsync(
                cost.TenantId, cost.ServiceOrigin, cost.Channel
            );
            
            if (budget == null) continue;
            
            var utilizationPercent = (double)cost.TotalCostMicros / budget.MonthlyBudgetMicros;
            
            // Check thresholds (80%, 100%)
            if (utilizationPercent >= 0.8 && !budget.Alert80Sent)
            {
                await SendBudgetAlertAsync(cost.TenantId, cost.ServiceOrigin, 80, utilizationPercent);
                budget.Alert80Sent = true;
            }
            
            if (utilizationPercent >= 1.0 && !budget.Alert100Sent)
            {
                await SendBudgetAlertAsync(cost.TenantId, cost.ServiceOrigin, 100, utilizationPercent);
                budget.Alert100Sent = true;
                
                // Publish budget exceeded event to Service Bus
                await _serviceBus.PublishAsync(new BudgetThresholdCrossedEvent
                {
                    TenantId = cost.TenantId,
                    ServiceOrigin = cost.ServiceOrigin,
                    Channel = cost.Channel,
                    ThresholdPercent = 100,
                    ActualPercent = utilizationPercent * 100,
                    CurrentSpend = cost.TotalCostMicros / 1000000.0m,
                    Budget = budget.MonthlyBudgetMicros / 1000000.0m,
                    Currency = cost.Currency
                });
            }
        }
        
        await _context.SaveChangesAsync();
    }
    
    private async Task SendBudgetAlertAsync(
        Guid tenantId, string serviceOrigin, int threshold, double actual)
    {
        // Send email + SignalR notification to admins
    }
}
```

**Budget Threshold Event:**
```json
{
  "specversion": "1.0",
  "type": "com.listoexpress.notifications.budget.threshold-crossed",
  "source": "https://notifications.listoexpress.com",
  "id": "evt-budget-001",
  "time": "2024-01-25T14:30:00Z",
  "datacontenttype": "application/json",
  "data": {
    "tenantId": "tenant-uuid-123",
    "serviceOrigin": "orders",
    "channel": "sms",
    "thresholdPercent": 100,
    "actualPercent": 105.3,
    "currentSpend": 52.65,
    "budget": 50.00,
    "currency": "USD"
  }
}
```

---

### 7.5. Admin Override with Audit Trail

**Admin Override Request:**
```http
POST /api/v1/notifications/send
Authorization: Bearer {admin_jwt_with_notifications:admin_scope}
X-Admin-Override: true
X-Tenant-Id: tenant-uuid-123
Content-Type: application/json

{
  "reason": "Critical system alert - payment failure notification",
  "scopeType": "tenant",
  "scopeId": "tenant-uuid-123",
  "ttlMinutes": 60,
  "notification": {
    "userId": "user-uuid-456",
    "channel": "sms",
    "templateKey": "payment_failed",
    "data": { ... }
  }
}
```

**Admin Override Validation:**
```csharp
public class AdminOverrideService
{
    public async Task<AdminOverrideResult> ValidateOverrideAsync(
        HttpContext context, AdminOverrideRequest request)
    {
        // 1. Verify JWT has notifications:admin scope
        if (!context.User.HasClaim("scope", "notifications:admin"))
        {
            return AdminOverrideResult.Forbidden("Missing admin scope");
        }
        
        // 2. Validate reason provided
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 20)
        {
            return AdminOverrideResult.Invalid("Reason must be at least 20 characters");
        }
        
        // 3. Create audit record
        var audit = new AdminOverrideAudit
        {
            OverrideId = Guid.NewGuid(),
            AdminUserId = context.User.FindFirst("sub")!.Value,
            TenantId = request.ScopeId,
            ScopeType = request.ScopeType,
            Reason = request.Reason,
            TtlMinutes = request.TtlMinutes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(request.TtlMinutes),
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };
        
        await _context.AdminOverrides.AddAsync(audit);
        await _context.SaveChangesAsync();
        
        // 4. Log to audit trail
        _logger.LogWarning(
            "Admin override granted: {OverrideId} by {AdminUserId} for {TenantId}. Reason: {Reason}",
            audit.OverrideId, audit.AdminUserId, audit.TenantId, audit.Reason
        );
        
        return AdminOverrideResult.Allowed(audit.OverrideId);
    }
}
```

**Audit Log Entry:**
```json
{
  "overrideId": "override-uuid-789",
  "adminUserId": "admin-uuid-111",
  "adminEmail": "admin@listoexpress.com",
  "tenantId": "tenant-uuid-123",
  "scopeType": "tenant",
  "reason": "Critical system alert - payment failure notification",
  "ttlMinutes": 60,
  "createdAt": "2024-01-25T14:30:00Z",
  "expiresAt": "2024-01-25T15:30:00Z",
  "ipAddress": "203.0.113.42",
  "userAgent": "Mozilla/5.0..."
}
```

---

### 7.6. Configuration (appsettings.json)

```json
{
  "RateLimiting": {
    "Redis": {
      "ConnectionString": "{{REDIS_CONNECTION_STRING}}",
      "DefaultDatabase": 0
    },
    "Defaults": {
      "PerUserHourly": {
        "Capacity": 60,
        "RefillRate": 60,
        "RefillIntervalSeconds": 3600,
        "BurstSize": 20,
        "MaxCap": 100
      },
      "PerServiceDaily": {
        "Email": {
          "Capacity": 50000,
          "MaxCap": 100000
        },
        "SMS": {
          "Capacity": 10000,
          "MaxCap": 20000
        },
        "Push": {
          "Capacity": 200000,
          "MaxCap": 500000
        },
        "InApp": {
          "Capacity": 999999999,
          "MaxCap": 999999999
        }
      }
    }
  },
  "Budgets": {
    "Defaults": {
      "MonthlyPerTenantUsd": {
        "Email": 100.00,
        "SMS": 500.00,
        "Push": 0.00
      },
      "AlertThresholds": [0.8, 1.0],
      "Currency": "USD"
    },
    "CostPerUnit": {
      "Email": {
        "Provider": "sendgrid",
        "UnitCostMicros": 950
      },
      "SMS": {
        "Provider": "twilio",
        "UnitCostMicros": 7900,
        "PerSegment": true
      },
      "Push": {
        "Provider": "fcm",
        "UnitCostMicros": 0
      }
    }
  },
  "AdminOverride": {
    "RequiredScope": "notifications:admin",
    "MinReasonLength": 20,
    "DefaultTtlMinutes": 60,
    "MaxTtlMinutes": 1440
  }
}
```

---

## 8. Notification Delivery Strategy

This section defines the delivery routing rules, retry policies with exponential backoff and jitter, provider failover strategies, webhook handlers, and template rendering.

### 8.1. Synchronous vs Asynchronous Routing

**Synchronous Delivery (< 2 second response):**
- **Use Cases:** Driver assignment (Orders, RideSharing), OTP/2FA, Critical system alerts
- **Timeout:** Hard timeout at 2 seconds
- **Retry:** Single attempt or limited retries (max 2)
- **Channels:** Push notifications + SignalR
- **Implementation:** Direct HTTP call to provider APIs

**Asynchronous Delivery (Queue-based):**
- **Use Cases:** Order confirmations, marketing, receipts, non-critical notifications
- **Timeout:** Configurable per channel (20-30 seconds)
- **Retry:** Full exponential backoff policy (up to 6 attempts)
- **Channels:** Email, SMS, Push (non-critical), In-App
- **Implementation:** Azure Service Bus → Azure Function → Provider API

**Priority Queue Tiers:**

| Priority | Queue Name | Use Cases | Processing |
|----------|------------|-----------|------------|
| **High** | `listo-notifications-priority` | OTP/2FA, Driver assignment, Security alerts | Real-time, max 2s latency |
| **Normal** | `listo-notifications-queue` | Order updates, Ride confirmations | Standard, max 30s latency |
| **Low** | `listo-notifications-bulk` | Marketing, Newsletters, Batch notifications | Batched, up to 5min latency |

**Routing Decision Logic:**
```csharp
public DeliveryMode DetermineDeliveryMode(NotificationRequest request)
{
    // Synchronous criteria
    if (request.TemplateKey.Contains("driver_assign") || 
        request.TemplateKey.Contains("otp") ||
        request.TemplateKey.Contains("2fa") ||
        request.Priority == "critical")
    {
        return DeliveryMode.Synchronous;
    }
    
    // Priority queue
    if (request.Priority == "high")
    {
        return DeliveryMode.PriorityQueue;
    }
    
    // Bulk
    if (request.IsBulk || request.Priority == "low")
    {
    return DeliveryMode.BulkQueue;
    }
    
    // Default: standard async
    return DeliveryMode.StandardQueue;
}
```

---

### 8.2. Channel-Specific Retry Policies

**Retry Policy Configuration (from Section 4.3):**

**OTP/SMS (Auth - High Priority):**
```json
{
  "serviceOrigin": "auth",
  "channel": "sms",
  "maxAttempts": 4,
  "baseDelaySeconds": 3,
  "backoffFactor": 2.0,
  "maxBackoffSeconds": 120,
  "jitterMs": 1000,
  "timeoutSeconds": 20
}
```

**Email (Standard):**
```json
{
  "serviceOrigin": "*",
  "channel": "email",
  "maxAttempts": 6,
  "baseDelaySeconds": 5,
  "backoffFactor": 2.0,
  "maxBackoffSeconds": 600,
  "jitterMs": 1000,
  "timeoutSeconds": 30
}
```

**Push Notifications:**
```json
{
  "serviceOrigin": "orders",
  "channel": "push",
  "maxAttempts": 3,
  "baseDelaySeconds": 2,
  "backoffFactor": 2.0,
  "maxBackoffSeconds": 300,
  "jitterMs": 500,
  "timeoutSeconds": 15
}
```

**Exponential Backoff with Jitter (.NET 9):**
```csharp
namespace Listo.Notification.Infrastructure.Retry;

public class ExponentialBackoffWithJitter
{
    private readonly Random _random = new();
    
    public TimeSpan CalculateDelay(
        int attemptNumber,
        int baseDelaySeconds,
        double backoffFactor,
        int maxBackoffSeconds,
        int jitterMs)
    {
        // Exponential backoff: baseDelay * (backoffFactor ^ attemptNumber)
        var exponentialDelay = baseDelaySeconds * Math.Pow(backoffFactor, attemptNumber);
        
        // Cap at max backoff
        var cappedDelay = Math.Min(exponentialDelay, maxBackoffSeconds);
        
        // Add random jitter to prevent thundering herd
        var jitterSeconds = _random.Next(0, jitterMs) / 1000.0;
        
        var totalDelay = cappedDelay + jitterSeconds;
        
        return TimeSpan.FromSeconds(totalDelay);
    }
    
    public async Task<TResult> ExecuteWithRetryAsync<TResult>(
        Func<Task<TResult>> operation,
        RetryPolicy policy,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        
        for (int attempt = 0; attempt < policy.MaxAttempts; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(policy.TimeoutSeconds));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                
                return await operation().WaitAsync(linked.Token);
            }
            catch (Exception ex) when (attempt < policy.MaxAttempts - 1)
            {
                lastException = ex;
                
                var delay = CalculateDelay(
                    attempt,
                    policy.BaseDelaySeconds,
                    policy.BackoffFactor,
                    policy.MaxBackoffSeconds,
                    policy.JitterMs
                );
                
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        
        throw new RetryExhaustedException(
            $"Operation failed after {policy.MaxAttempts} attempts",
            lastException
        );
    }
}

public record RetryPolicy(
    int MaxAttempts,
    int BaseDelaySeconds,
    double BackoffFactor,
    int MaxBackoffSeconds,
    int JitterMs,
    int TimeoutSeconds
);
```

---

### 8.3. Provider Failover Strategy

**Failover Configuration:**

**SMS Channel:**
- **Primary:** Twilio (US/Canada/International)
- **Secondary:** AWS SNS (US/Canada fallback)
- **Circuit Breaker:** 5 consecutive failures → open for 60 seconds
- **Health Check:** Probe every 30 seconds when circuit open

**Email Channel:**
- **Primary:** SendGrid
- **Secondary:** Azure Communication Services
- **Circuit Breaker:** 5 consecutive failures → open for 60 seconds
- **Health Check:** Probe every 30 seconds when circuit open

**Push Channel:**
- **Primary:** Firebase Cloud Messaging (FCM)
- **Secondary:** None (FCM has high availability)
- **Circuit Breaker:** 10 consecutive failures → open for 120 seconds

**Circuit Breaker Implementation:**
```csharp
using Polly;
using Polly.CircuitBreaker;

namespace Listo.Notification.Infrastructure.CircuitBreaker;

public class ProviderCircuitBreakerFactory
{
    private readonly Dictionary<string, AsyncCircuitBreakerPolicy> _breakers = new();
    private readonly ILogger<ProviderCircuitBreakerFactory> _logger;
    
    public AsyncCircuitBreakerPolicy GetOrCreate(string providerName, CircuitBreakerConfig config)
    {
        if (_breakers.TryGetValue(providerName, out var breaker))
        {
            return breaker;
        }
        
        var newBreaker = Policy
            .Handle<Exception>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: config.FailureThreshold, // 0.5 = 50% failures
                samplingDuration: TimeSpan.FromSeconds(config.SamplingDurationSeconds),
                minimumThroughput: config.MinimumThroughput, // min requests to evaluate
                durationOfBreak: TimeSpan.FromSeconds(config.BreakDurationSeconds),
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for {Provider}. Duration: {Duration}s. Exception: {Exception}",
                        providerName, duration.TotalSeconds, exception.Message
                    );
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for {Provider}", providerName);
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open for {Provider}", providerName);
                }
            );
        
        _breakers[providerName] = newBreaker;
        return newBreaker;
    }
}

public record CircuitBreakerConfig(
    double FailureThreshold,
    int SamplingDurationSeconds,
    int MinimumThroughput,
    int BreakDurationSeconds
);
```

**Failover Flow:**
```mermaid
flowchart TD
    A[Notification Request] --> B{Primary Provider Available?}
    B -->|Circuit Closed| C[Attempt Primary]
    B -->|Circuit Open| D{Secondary Configured?}
    C -->|Success| E[Log Success]
    C -->|Failure| F{Max Retries Reached?}
    F -->|No| G[Exponential Backoff]
    G --> C
    F -->|Yes| H[Open Circuit]
    H --> D
    D -->|Yes| I[Attempt Secondary]
    D -->|No| J[Mark Failed - Queue for Retry]
    I -->|Success| K[Log Failover Event]
    I -->|Failure| J
    E --> L[Update NotificationQueue]
    K --> L
    J --> M[Publish Failed Event]
```

---

### 8.4. Webhook Handlers

**8.4.1. Twilio SMS Status Webhooks**

**Endpoint:** `POST /api/v1/webhooks/twilio/sms-status`

**Signature Validation:**
```csharp
using System.Security.Cryptography;
using System.Text;

public class TwilioWebhookValidator
{
    private readonly string _authToken;
    
    public bool ValidateSignature(HttpRequest request, string twilioSignature)
    {
        var url = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        var data = new StringBuilder(url);
        
        // Add all POST parameters in alphabetical order
        foreach (var key in request.Form.Keys.OrderBy(k => k))
        {
            data.Append(key).Append(request.Form[key]);
        }
        
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_authToken));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()));
        var expectedSignature = Convert.ToBase64String(hash);
        
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(twilioSignature)
        );
    }
}
```

**Event Types:**
- `queued`, `sent`, `delivered`, `failed`, `undelivered`

**8.4.2. SendGrid Event Webhooks**

**Endpoint:** `POST /api/v1/webhooks/sendgrid/events`

**Event Types:**
- `delivered`, `opened`, `clicked`, `bounce`, `dropped`, `spam_report`, `unsubscribe`

**Signature Validation:**
```csharp
public class SendGridWebhookValidator
{
    public bool ValidateSignature(
        string publicKey,
        string signature,
        string timestamp,
        string payload)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        
        var dataToVerify = Encoding.UTF8.GetBytes(timestamp + payload);
        var signatureBytes = Convert.FromBase64String(signature);
        
        return ecdsa.VerifyData(dataToVerify, signatureBytes, HashAlgorithmName.SHA256);
    }
}
```

**8.4.3. FCM Delivery Receipts (GCP Pub/Sub)**

**Endpoint:** `POST /api/v1/webhooks/fcm/events`

**Setup:**
1. Configure GCP Pub/Sub topic: `fcm-delivery-receipts`
2. Create push subscription to `/api/v1/webhooks/fcm/events`
3. Verify JWT signature from GCP

**Event Types:**
- `MESSAGE_DELIVERED`, `MESSAGE_CLICKED`, `MESSAGE_EXPIRED`

---

### 8.5. Template Rendering with Scriban

**Template Engine:** Scriban (Liquid-like syntax)

**Localization Fallback Strategy:**
1. Try exact locale (e.g., `en-US`)
2. Try language only (e.g., `en`)
3. Fall back to `en-US` (default)

**Template Example:**
```liquid
Hi {{user.firstName}},

Your order {{order.id}} has been confirmed!

{{ if order.estimatedDelivery }}
Estimated delivery: {{order.estimatedDelivery | date.format "%B %d at %I:%M %p"}}
{{ end }}

Total: {{order.total | currency}}

Track your order: {{order.trackingLink}}

Thank you,
The {{company.name}} Team
```

**Safe Rendering (.NET 9):**
```csharp
using Scriban;
using Scriban.Runtime;

public class TemplateRenderer
{
    public async Task<string> RenderAsync(
        string templateBody,
        Dictionary<string, object> variables,
        string locale)
    {
        var template = Template.Parse(templateBody);
        
        if (template.HasErrors)
        {
            throw new TemplateParsingException(
                string.Join(", ", template.Messages.Select(m => m.Message))
            );
        }
        
        var context = new TemplateContext
        {
            EnableRelaxedMemberAccess = true,
            StrictVariables = true // Throw on missing variables
        };
        
        // Add custom functions
        var scriptObject = new ScriptObject();
        scriptObject.Import(typeof(CustomFunctions));
        context.PushGlobal(scriptObject);
        
        // Add variables
        foreach (var (key, value) in variables)
        {
            context.SetValue(new ScriptVariable(key), value);
        }
        
        // Render with escaping enabled
        return await template.RenderAsync(context);
    }
}
```

---

### 8.6. Delivery Orchestration Flow

```mermaid
sequenceDiagram
    participant API as Notification API
    participant Queue as Service Bus Queue
    participant Func as Azure Function
    participant RateLimit as Rate Limiter
    participant CircuitBreaker as Circuit Breaker
    participant Primary as Primary Provider
    participant Secondary as Secondary Provider
    participant Webhook as Webhook Handler
    
    API->>Queue: Enqueue Notification
    Queue->>Func: Trigger Function
    Func->>RateLimit: Check Rate Limit
    alt Rate Limit Exceeded
        RateLimit-->>Func: 429 Denied
        Func->>Queue: Requeue with Delay
    else Rate Limit OK
        RateLimit-->>Func: Allowed
        Func->>CircuitBreaker: Check Circuit State
        alt Circuit Open
            CircuitBreaker-->>Func: Use Secondary
            Func->>Secondary: Send Notification
            Secondary-->>Func: Response
        else Circuit Closed
            CircuitBreaker-->>Func: Use Primary
            Func->>Primary: Send Notification
            alt Primary Success
                Primary-->>Func: Success
                Func->>Func: Log Delivery
            else Primary Failure
                Primary-->>Func: Error
                Func->>CircuitBreaker: Record Failure
                alt Should Retry
                    Func->>Queue: Requeue with Backoff
                else Max Retries
                    Func->>Secondary: Attempt Failover
                end
            end
        end
    end
    Primary->>Webhook: Delivery Status Update
    Webhook->>Func: Process Webhook
    Func->>Func: Update NotificationQueue Status
```

---

## 9. API Implementation

This section outlines the API layer structure, controller design, endpoint implementations, and integration with the application and infrastructure layers.

### 9.1. Project Structure

```
Listo.Notification/
├── Listo.Notification.API/
│   ├── Controllers/
│   │   ├── NotificationsController.cs
│   │   ├── DevicesController.cs
│   │   ├── MessagesController.cs
│   │   ├── ConversationsController.cs
│   │   ├── PreferencesController.cs
│   │   ├── TemplatesController.cs
│   │   ├── AnalyticsController.cs
│   │   ├── HealthController.cs
│   │   └── Internal/
│   │       ├── InternalNotificationsController.cs
│   │       └── ServiceBusWebhooksController.cs
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   ├── ServiceAuthenticationMiddleware.cs
│   │   └── RequestLoggingMiddleware.cs
│   ├── Filters/
│   │   ├── ValidateModelStateAttribute.cs
│   │   └── ApiKeyAuthorizationAttribute.cs
│   ├── Extensions/
│   │   ├── ServiceCollectionExtensions.cs
│   │   └── ApplicationBuilderExtensions.cs
│   ├── Models/
│   │   ├── Requests/
│   │   ├── Responses/
│   │   └── DTOs/
│   ├── Program.cs
│   └── appsettings.json
│
├── Listo.Notification.Application/
│   ├── Services/
│   │   ├── NotificationService.cs
│   │   ├── DeviceService.cs
│   │   ├── MessageService.cs
│   │   ├── TemplateService.cs
│   │   ├── AnalyticsService.cs
│   │   └── Interfaces/
│   ├── Commands/
│   ├── Queries/
│   ├── Validators/
│   └── Mappings/
│
└── Listo.Notification.Infrastructure/
    ├── Data/
    │   ├── NotificationDbContext.cs
    │   └── Repositories/
    ├── Providers/
    │   ├── FcmPushProvider.cs
    │   ├── TwilioSmsProvider.cs
    │   ├── SendGridEmailProvider.cs
    │   └── SignalRProvider.cs
    ├── ServiceBus/
    │   ├── ServiceBusProcessor.cs
    │   └── ServiceBusPublisher.cs
    └── Storage/
        └── AzureBlobStorageService.cs
```

---

### 9.2. Controller Design Patterns

#### Base Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.Application.Common;

namespace Listo.Notification.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ApiResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(new ApiSuccessResponse<T>
            {
                Success = true,
                Data = result.Value,
                Message = result.Message
            });
        }

        return result.ErrorType switch
        {
            ErrorType.NotFound => NotFound(new ApiErrorResponse
            {
                Success = false,
                Error = result.Error,
                Message = result.Message
            }),
            ErrorType.Validation => BadRequest(new ApiErrorResponse
            {
                Success = false,
                Error = result.Error,
                Message = result.Message,
                ValidationErrors = result.ValidationErrors
            }),
            ErrorType.Unauthorized => Unauthorized(new ApiErrorResponse
            {
                Success = false,
                Error = "Unauthorized",
                Message = result.Message
            }),
            _ => StatusCode(500, new ApiErrorResponse
            {
                Success = false,
                Error = "InternalServerError",
                Message = result.Message
            })
        };
    }

    protected IActionResult ApiResponse(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok(new ApiSuccessResponse
            {
                Success = true,
                Message = result.Message
            });
        }

        return BadRequest(new ApiErrorResponse
        {
            Success = false,
            Error = result.Error,
            Message = result.Message
        });
    }
}
```

---

### 9.3. NotificationsController Implementation

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.Application.Services.Interfaces;
using Listo.Notification.API.Models.Requests;

namespace Listo.Notification.API.Controllers;

[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get user notifications with pagination and filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<NotificationDto>), 200)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] NotificationQueryParameters parameters)
    {
        var userId = User.GetUserId();
        var result = await _notificationService.GetUserNotificationsAsync(
            userId, parameters);
        return ApiResponse(result);
    }

    /// <summary>
    /// Get notification by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NotificationDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetNotification(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _notificationService.GetNotificationByIdAsync(
            userId, id);
        return ApiResponse(result);
    }

    /// <summary>
    /// Send a notification to a user
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,System")]
    [ProducesResponseType(typeof(NotificationDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SendNotification(
        [FromBody] SendNotificationRequest request)
    {
        var result = await _notificationService.SendNotificationAsync(request);
        
        if (result.IsSuccess)
        {
            return CreatedAtAction(
                nameof(GetNotification),
                new { id = result.Value.Id },
                result.Value);
        }
        
        return ApiResponse(result);
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPatch("{id}/read")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _notificationService.MarkAsReadAsync(userId, id);
        
        if (result.IsSuccess)
            return NoContent();
        
        return ApiResponse(result);
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// Delete notification
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _notificationService.DeleteNotificationAsync(
            userId, id);
        
        if (result.IsSuccess)
            return NoContent();
        
        return ApiResponse(result);
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    [HttpGet("unread/count")]
    [ProducesResponseType(typeof(int), 200)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.GetUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }
}
```

---

### 9.4. Internal NotificationsController (Service-to-Service)

```csharp
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.API.Filters;
using Listo.Notification.Application.Services.Interfaces;
using Listo.Notification.API.Models.Requests;

namespace Listo.Notification.API.Controllers.Internal;

[Route("api/v1/internal/notifications")]
[ServiceKeyAuthorization]
public class InternalNotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<InternalNotificationsController> _logger;

    public InternalNotificationsController(
        INotificationService notificationService,
        ILogger<InternalNotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Queue notification for processing (used by other services)
    /// </summary>
    [HttpPost("queue")]
    [ProducesResponseType(typeof(QueueNotificationResponse), 200)]
    [ProducesResponseType(typeof(QueueNotificationResponse), 202)]
    public async Task<IActionResult> QueueNotification(
        [FromBody] QueueNotificationRequest request,
        [FromHeader(Name = "X-Service-Secret")] string serviceSecret,
        [FromHeader(Name = "X-Correlation-Id")] string correlationId,
        [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey)
    {
        // For synchronous requests, process immediately and return delivery status
        if (request.Synchronous)
        {
            var result = await _notificationService
                .ProcessNotificationSynchronouslyAsync(
                    request, correlationId, idempotencyKey);
            
            return Ok(new QueueNotificationResponse
            {
                NotificationId = result.NotificationId,
                Status = result.Status,
                Channels = result.ChannelResults,
                ProcessingTimeMs = result.ProcessingTimeMs
            });
        }

        // For async requests, queue and return immediately
        var notificationId = await _notificationService
            .QueueNotificationAsync(request, correlationId, idempotencyKey);
        
        return Accepted(new QueueNotificationResponse
        {
            NotificationId = notificationId,
            Status = "queued",
            Message = "Notification queued for processing"
        });
    }

    /// <summary>
    /// Batch queue notifications (for bulk operations)
    /// </summary>
    [HttpPost("queue/batch")]
    [ProducesResponseType(typeof(BatchQueueResponse), 202)]
    public async Task<IActionResult> QueueNotificationBatch(
        [FromBody] QueueNotificationBatchRequest request,
        [FromHeader(Name = "X-Service-Secret")] string serviceSecret,
        [FromHeader(Name = "X-Correlation-Id")] string correlationId)
    {
        var result = await _notificationService
            .QueueNotificationBatchAsync(request, correlationId);
        
        return Accepted(new BatchQueueResponse
        {
            BatchId = result.BatchId,
            TotalQueued = result.TotalQueued,
            Status = "queued"
        });
    }
}
```

---

### 9.5. DevicesController Implementation

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.Application.Services.Interfaces;
using Listo.Notification.API.Models.Requests;

namespace Listo.Notification.API.Controllers;

[Authorize]
public class DevicesController : ApiControllerBase
{
    private readonly IDeviceService _deviceService;

    public DevicesController(IDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    /// <summary>
    /// Register or update a device for push notifications
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceDto), 200)]
    [ProducesResponseType(typeof(DeviceDto), 201)]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceRequest request)
    {
        var userId = User.GetUserId();
        var result = await _deviceService.RegisterDeviceAsync(userId, request);
        
        if (result.IsSuccess)
        {
            return result.IsNewDevice
                ? CreatedAtAction(nameof(GetDevice), 
                    new { id = result.Value.Id }, result.Value)
                : Ok(result.Value);
        }
        
        return ApiResponse(result);
    }

    /// <summary>
    /// Get user's registered devices
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DeviceDto>), 200)]
    public async Task<IActionResult> GetDevices()
    {
        var userId = User.GetUserId();
        var result = await _deviceService.GetUserDevicesAsync(userId);
        return ApiResponse(result);
    }

    /// <summary>
    /// Get device by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DeviceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDevice(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _deviceService.GetDeviceByIdAsync(userId, id);
        return ApiResponse(result);
    }

    /// <summary>
    /// Unregister device
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnregisterDevice(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _deviceService.UnregisterDeviceAsync(userId, id);
        
        if (result.IsSuccess)
            return NoContent();
        
        return ApiResponse(result);
    }

    /// <summary>
    /// Update device token (for token refresh scenarios)
    /// </summary>
    [HttpPatch("{id}/token")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateDeviceToken(
        Guid id,
        [FromBody] UpdateDeviceTokenRequest request)
    {
        var userId = User.GetUserId();
        var result = await _deviceService.UpdateDeviceTokenAsync(
            userId, id, request.NewToken);
        
        if (result.IsSuccess)
            return NoContent();
        
        return ApiResponse(result);
    }
}
```

---

### 9.6. ConversationsController Implementation

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.Application.Services.Interfaces;
using Listo.Notification.API.Models.Requests;

namespace Listo.Notification.API.Controllers;

[Authorize]
public class ConversationsController : ApiControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IBlobStorageService _blobStorageService;

    public ConversationsController(
        IMessageService messageService,
        IBlobStorageService blobStorageService)
    {
        _messageService = messageService;
        _blobStorageService = blobStorageService;
    }

    /// <summary>
    /// Get user conversations with pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<ConversationDto>), 200)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] PaginationParameters parameters)
    {
        var userId = User.GetUserId();
        var result = await _messageService.GetConversationsAsync(
            userId, parameters);
        return ApiResponse(result);
    }

    /// <summary>
    /// Get conversation by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConversationDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetConversation(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _messageService.GetConversationByIdAsync(
            userId, id);
        return ApiResponse(result);
    }

    /// <summary>
    /// Create new conversation
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConversationDto), 201)]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateConversationRequest request)
    {
        var userId = User.GetUserId();
        var result = await _messageService.CreateConversationAsync(
            userId, request);
        
        if (result.IsSuccess)
        {
            return CreatedAtAction(
                nameof(GetConversation),
                new { id = result.Value.Id },
                result.Value);
        }
        
        return ApiResponse(result);
    }

    /// <summary>
    /// Get messages in conversation
    /// </summary>
    [HttpGet("{id}/messages")]
    [ProducesResponseType(typeof(PaginatedResponse<MessageDto>), 200)]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] PaginationParameters parameters)
    {
        var userId = User.GetUserId();
        var result = await _messageService.GetConversationMessagesAsync(
            userId, id, parameters);
        return ApiResponse(result);
    }

    /// <summary>
    /// Send message with optional file attachment
    /// </summary>
    [HttpPost("{id}/messages")]
    [ProducesResponseType(typeof(MessageDto), 201)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromForm] SendMessageRequest request)
    {
        var userId = User.GetUserId();
        
        // Handle file upload if present
        string? fileUrl = null;
        if (request.File != null)
        {
            var uploadResult = await _blobStorageService.UploadFileAsync(
                request.File,
                $"messages/{id}",
                userId);
            
            if (!uploadResult.IsSuccess)
                return ApiResponse(uploadResult);
            
            fileUrl = uploadResult.Value.Url;
        }

        var result = await _messageService.SendMessageAsync(
            userId, id, request.Content, fileUrl);
        
        if (result.IsSuccess)
        {
            return CreatedAtAction(
                nameof(GetMessage),
                new { conversationId = id, messageId = result.Value.Id },
                result.Value);
        }
        
        return ApiResponse(result);
    }

    /// <summary>
    /// Get message by ID
    /// </summary>
    [HttpGet("{conversationId}/messages/{messageId}")]
    [ProducesResponseType(typeof(MessageDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMessage(
        Guid conversationId,
        Guid messageId)
    {
        var userId = User.GetUserId();
        var result = await _messageService.GetMessageByIdAsync(
            userId, conversationId, messageId);
        return ApiResponse(result);
    }

    /// <summary>
    /// Mark conversation as read
    /// </summary>
    [HttpPatch("{id}/read")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = User.GetUserId();
        await _messageService.MarkConversationAsReadAsync(userId, id);
        return NoContent();
    }
}
```

---

### 9.7. PreferencesController Implementation

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.Application.Services.Interfaces;
using Listo.Notification.API.Models.Requests;

namespace Listo.Notification.API.Controllers;

[Authorize]
public class PreferencesController : ApiControllerBase
{
    private readonly IPreferencesService _preferencesService;

    public PreferencesController(IPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
    }

    /// <summary>
    /// Get user notification preferences
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserPreferencesDto), 200)]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = User.GetUserId();
        var result = await _preferencesService.GetPreferencesAsync(userId);
        return ApiResponse(result);
    }

    /// <summary>
    /// Update notification preferences
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserPreferencesDto), 200)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdatePreferencesRequest request)
    {
        var userId = User.GetUserId();
        var result = await _preferencesService.UpdatePreferencesAsync(
            userId, request);
        return ApiResponse(result);
    }

    /// <summary>
    /// Update channel preference (enable/disable specific channel)
    /// </summary>
    [HttpPatch("channels/{channel}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> UpdateChannelPreference(
        string channel,
        [FromBody] UpdateChannelPreferenceRequest request)
    {
        var userId = User.GetUserId();
        var result = await _preferencesService.UpdateChannelPreferenceAsync(
            userId, channel, request.Enabled);
        
        if (result.IsSuccess)
            return NoContent();
        
        return ApiResponse(result);
    }
}
```

---

### 9.8. AnalyticsController Implementation

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Listo.Notification.Application.Services.Interfaces;
using Listo.Notification.API.Models.Requests;

namespace Listo.Notification.API.Controllers;

[Authorize(Roles = "Admin")]
public class AnalyticsController : ApiControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Get notification delivery metrics
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(NotificationMetricsDto), 200)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] MetricsQueryParameters parameters)
    {
        var result = await _analyticsService.GetMetricsAsync(parameters);
        return ApiResponse(result);
    }

    /// <summary>
    /// Get delivery statistics by channel
    /// </summary>
    [HttpGet("stats/channels")]
    [ProducesResponseType(typeof(ChannelStatsDto), 200)]
    public async Task<IActionResult> GetChannelStats(
        [FromQuery] DateRangeParameters parameters)
    {
        var result = await _analyticsService.GetChannelStatsAsync(parameters);
        return ApiResponse(result);
    }

    /// <summary>
    /// Get user engagement metrics
    /// </summary>
    [HttpGet("engagement")]
    [ProducesResponseType(typeof(EngagementMetricsDto), 200)]
    public async Task<IActionResult> GetEngagementMetrics(
        [FromQuery] DateRangeParameters parameters)
    {
        var result = await _analyticsService
            .GetEngagementMetricsAsync(parameters);
        return ApiResponse(result);
    }
}
```

---

### 9.9. HealthController Implementation

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Listo.Notification.API.Controllers;

[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Basic health check endpoint
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Detailed health check with dependency status
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(HealthReport), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetDetailed()
    {
        var report = await _healthCheckService.CheckHealthAsync();
        
        var response = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                data = e.Value.Data
            })
        };

        return report.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(503, response);
    }
}
```

---

### 9.10. Application Layer Service Example

```csharp
using Listo.Notification.Application.Services.Interfaces;
using Listo.Notification.Domain.Entities;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Listo.Notification.Application.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _context;
    private readonly IFcmPushProvider _fcmProvider;
    private readonly ISignalRProvider _signalRProvider;
    private readonly ITemplateService _templateService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        NotificationDbContext context,
        IFcmPushProvider fcmProvider,
        ISignalRProvider signalRProvider,
        ITemplateService templateService,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _fcmProvider = fcmProvider;
        _signalRProvider = signalRProvider;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task<Result<PaginatedResponse<NotificationDto>>> 
        GetUserNotificationsAsync(
            Guid userId,
            NotificationQueryParameters parameters)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        // Apply filters
        if (parameters.IsRead.HasValue)
            query = query.Where(n => n.IsRead == parameters.IsRead.Value);

        if (parameters.Priority.HasValue)
            query = query.Where(n => n.Priority == parameters.Priority.Value);

        if (parameters.Channel != null)
            query = query.Where(n => n.Channel == parameters.Channel);

        // Total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = parameters.SortBy?.ToLower() switch
        {
            "createdat" => parameters.IsDescending
                ? query.OrderByDescending(n => n.CreatedAt)
                : query.OrderBy(n => n.CreatedAt),
            "priority" => parameters.IsDescending
                ? query.OrderByDescending(n => n.Priority)
                : query.OrderBy(n => n.Priority),
            _ => query.OrderByDescending(n => n.CreatedAt)
        };

        // Apply pagination
        var notifications = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Title = n.Title,
                Body = n.Body,
                Channel = n.Channel,
                Priority = n.Priority,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
                Data = n.Data
            })
            .ToListAsync();

        return Result<PaginatedResponse<NotificationDto>>.Success(
            new PaginatedResponse<NotificationDto>
            {
                Items = notifications,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)parameters.PageSize)
            });
    }

    public async Task<SynchronousDeliveryResult> 
        ProcessNotificationSynchronouslyAsync(
            QueueNotificationRequest request,
            string correlationId,
            string idempotencyKey)
    {
        var stopwatch = Stopwatch.StartNew();
        var channelResults = new Dictionary<string, ChannelDeliveryResult>();

        try
        {
            // Render template
            var template = await _templateService
                .GetTemplateAsync(request.TemplateKey);
            var renderedContent = _templateService.RenderTemplate(
                template, request.Data);

            // Create notification record
            var notification = new Domain.Entities.Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Title = renderedContent.Title,
                Body = renderedContent.Body,
                Channel = request.Channel,
                Priority = request.Priority,
                CorrelationId = correlationId,
                IdempotencyKey = idempotencyKey,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();

            // Send via channels in parallel
            var tasks = new List<Task<ChannelDeliveryResult>>();

            if (request.Channel == "push")
            {
                tasks.Add(SendPushNotificationAsync(
                    notification, request.EncryptedFirebaseToken));
            }

            // Always broadcast via SignalR for real-time updates
            tasks.Add(BroadcastViaSignalRAsync(notification));

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                channelResults[result.Channel] = result;
            }

            stopwatch.Stop();

            return new SynchronousDeliveryResult
            {
                NotificationId = notification.Id,
                Status = channelResults.All(r => r.Value.Delivered)
                    ? "delivered"
                    : "partial",
                ChannelResults = channelResults,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error processing synchronous notification. CorrelationId: {CorrelationId}",
                correlationId);
            throw;
        }
    }

    private async Task<ChannelDeliveryResult> SendPushNotificationAsync(
        Domain.Entities.Notification notification,
        string encryptedToken)
    {
        try
        {
            var result = await _fcmProvider.SendNotificationAsync(
                encryptedToken,
                notification.Title,
                notification.Body,
                notification.Data);

            return new ChannelDeliveryResult
            {
                Channel = "push",
                Delivered = result.IsSuccess,
                ProviderMessageId = result.MessageId,
                DeliveredAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM push notification failed");
            return new ChannelDeliveryResult
            {
                Channel = "push",
                Delivered = false,
                Error = ex.Message
            };
        }
    }

    private async Task<ChannelDeliveryResult> BroadcastViaSignalRAsync(
        Domain.Entities.Notification notification)
    {
        try
        {
            await _signalRProvider.BroadcastNotificationAsync(
                notification.UserId.ToString(),
                notification);

            return new ChannelDeliveryResult
            {
                Channel = "signalr",
                Broadcast = true,
                DeliveredAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignalR broadcast failed");
            return new ChannelDeliveryResult
            {
                Channel = "signalr",
                Broadcast = false,
                Error = ex.Message
            };
        }
    }
}
```

---

### 9.11. Middleware Implementation

#### ExceptionHandlingMiddleware

```csharp
using System.Net;
using System.Text.Json;
using Listo.Notification.API.Models.Responses;

namespace Listo.Notification.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unhandled exception occurred. Path: {Path}",
                context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var statusCode = exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        var response = new ApiErrorResponse
        {
            Success = false,
            Error = statusCode.ToString(),
            Message = exception.Message,
            TraceId = context.TraceIdentifier
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response);
        return context.Response.WriteAsync(json);
    }
}
```

#### ServiceAuthenticationMiddleware

```csharp
using Microsoft.Extensions.Options;

namespace Listo.Notification.API.Middleware;

public class ServiceAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ServiceAuthenticationMiddleware> _logger;
    private readonly ServiceSecretsConfig _config;

    public ServiceAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ServiceAuthenticationMiddleware> logger,
        IOptions<ServiceSecretsConfig> config)
    {
        _next = next;
        _logger = logger;
        _config = config.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to /api/v1/internal/* routes
        if (context.Request.Path.StartsWithSegments("/api/v1/internal"))
        {
            if (!context.Request.Headers
                .TryGetValue("X-Service-Secret", out var serviceSecret))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync(
                    "Missing X-Service-Secret header");
                return;
            }

            var serviceOrigin = context.Request.Headers["X-Service-Origin"]
                .ToString();

            if (!ValidateServiceSecret(serviceOrigin, serviceSecret!))
            {
                _logger.LogWarning(
                    "Invalid service secret for origin: {Origin}",
                    serviceOrigin);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid service secret");
                return;
            }
        }

        await _next(context);
    }

    private bool ValidateServiceSecret(string origin, string secret)
    {
        return origin?.ToLower() switch
        {
            "auth" => secret == _config.AuthServiceSecret,
            "orders" => secret == _config.OrdersServiceSecret,
            "ridesharing" => secret == _config.RideSharingServiceSecret,
            _ => false
        };
    }
}
```

---

### 9.12. Program.cs Configuration

```csharp
using Listo.Notification.API.Extensions;
using Listo.Notification.API.Middleware;
using Listo.Notification.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication & Authorization
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization();

// Application services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>()
    .AddAzureServiceBusQueue(
        builder.Configuration["ServiceBus:ConnectionString"],
        builder.Configuration["ServiceBus:QueueName"])
    .AddAzureBlobStorage(
        builder.Configuration["BlobStorage:ConnectionString"]);

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins")
                    .Get<string[]>()!)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ServiceAuthenticationMiddleware>();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/detailed");

app.Run();
```

---

### 9.13. API Response Models

```csharp
namespace Listo.Notification.API.Models.Responses;

public class ApiSuccessResponse<T>
{
    public bool Success { get; set; } = true;
    public T? Data { get; set; }
    public string? Message { get; set; }
}

public class ApiSuccessResponse
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}

public class ApiErrorResponse
{
    public bool Success { get; set; } = false;
    public string Error { get; set; } = null!;
    public string Message { get; set; } = null!;
    public Dictionary<string, string[]>? ValidationErrors { get; set; }
    public string? TraceId { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
```

---

## 10. Validation & Error Handling

This section covers comprehensive input validation, error handling patterns, and consistent response formatting.

### 10.1. FluentValidation Setup

**Installation:**
```bash
dotnet add package FluentValidation.AspNetCore
```

**Configuration (Program.cs):**
```csharp
using FluentValidation;
using FluentValidation.AspNetCore;

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Disable default ModelState validation to use FluentValidation exclusively
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
```

---

### 10.2. Request Validators

#### SendNotificationRequestValidator

```csharp
using FluentValidation;
using Listo.Notification.API.Models.Requests;

namespace Listo.Notification.Application.Validators;

public class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required")
            .Must(BeValidGuid).WithMessage("UserId must be a valid GUID");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required")
            .Must(BeValidChannel).WithMessage("Channel must be push, sms, email, or inApp");

        RuleFor(x => x.TemplateKey)
            .NotEmpty().WithMessage("TemplateKey is required")
            .MaximumLength(100).WithMessage("TemplateKey must not exceed 100 characters")
            .Matches("^[a-z0-9_]+$").WithMessage("TemplateKey must be lowercase alphanumeric with underscores");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required")
            .Must(BeValidPriority).WithMessage("Priority must be high, normal, or low");

        RuleFor(x => x.Data)
            .NotNull().WithMessage("Data is required")
            .Must(dict => dict.Count > 0).WithMessage("Data must contain at least one key-value pair");

        When(x => x.ScheduledAt.HasValue, () =>
        {
            RuleFor(x => x.ScheduledAt)
                .Must(BeInFuture).WithMessage("ScheduledAt must be in the future");
        });
    }

    private bool BeValidGuid(string? value) => 
        !string.IsNullOrEmpty(value) && Guid.TryParse(value, out _);

    private bool BeValidChannel(string? channel) =>
        new[] { "push", "sms", "email", "inApp" }.Contains(channel?.ToLower());

    private bool BeValidPriority(string? priority) =>
        new[] { "high", "normal", "low" }.Contains(priority?.ToLower());

    private bool BeInFuture(DateTime? dateTime) =>
        dateTime > DateTime.UtcNow;
}
```

#### RegisterDeviceRequestValidator

```csharp
public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceToken)
            .NotEmpty().WithMessage("DeviceToken is required")
            .MinimumLength(10).WithMessage("DeviceToken appears to be invalid (too short)")
            .MaximumLength(500).WithMessage("DeviceToken exceeds maximum length");

        RuleFor(x => x.Platform)
            .NotEmpty().WithMessage("Platform is required")
            .Must(BeValidPlatform).WithMessage("Platform must be android, ios, or web");

        RuleFor(x => x.DeviceInfo)
            .Must(BeValidJson).When(x => !string.IsNullOrEmpty(x.DeviceInfo))
            .WithMessage("DeviceInfo must be valid JSON");
    }

    private bool BeValidPlatform(string? platform) =>
        new[] { "android", "ios", "web" }.Contains(platform?.ToLower());

    private bool BeValidJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return true;
        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

#### QueueNotificationRequestValidator

```csharp
public class QueueNotificationRequestValidator : AbstractValidator<QueueNotificationRequest>
{
    public QueueNotificationRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required")
            .Must(BeValidGuid).WithMessage("UserId must be a valid GUID");

        RuleFor(x => x.ServiceOrigin)
            .NotEmpty().WithMessage("ServiceOrigin is required")
            .Must(BeValidServiceOrigin).WithMessage("ServiceOrigin must be auth, orders, ridesharing, products, or system");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required")
            .Must(BeValidChannel).WithMessage("Channel must be push, sms, email, or inApp");

        RuleFor(x => x.TemplateKey)
            .NotEmpty().WithMessage("TemplateKey is required")
            .MaximumLength(100).WithMessage("TemplateKey must not exceed 100 characters");

        RuleFor(x => x.Priority)
            .NotEmpty().WithMessage("Priority is required")
            .Must(BeValidPriority).WithMessage("Priority must be high, normal, or low");

        When(x => x.Synchronous, () =>
        {
            RuleFor(x => x.Channel)
                .Equal("push").WithMessage("Synchronous delivery only supports push channel");

            RuleFor(x => x.EncryptedFirebaseToken)
                .NotEmpty().WithMessage("EncryptedFirebaseToken is required for synchronous push notifications");
        });

        When(x => x.Channel == "email", () =>
        {
            RuleFor(x => x.EncryptedEmail)
                .NotEmpty().WithMessage("EncryptedEmail is required for email channel");
        });

        When(x => x.Channel == "sms", () =>
        {
            RuleFor(x => x.EncryptedPhoneNumber)
                .NotEmpty().WithMessage("EncryptedPhoneNumber is required for SMS channel");
        });
    }

    private bool BeValidGuid(string? value) => 
        !string.IsNullOrEmpty(value) && Guid.TryParse(value, out _);

    private bool BeValidChannel(string? channel) =>
        new[] { "push", "sms", "email", "inApp" }.Contains(channel?.ToLower());

    private bool BeValidPriority(string? priority) =>
        new[] { "high", "normal", "low" }.Contains(priority?.ToLower());

    private bool BeValidServiceOrigin(string? origin) =>
        new[] { "auth", "orders", "ridesharing", "products", "system" }.Contains(origin?.ToLower());
}
```

---

### 10.3. Validation Filter Attribute

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Listo.Notification.API.Filters;

public class ValidateModelStateAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );

            var response = new ApiErrorResponse
            {
                Success = false,
                Error = "ValidationError",
                Message = "One or more validation errors occurred",
                ValidationErrors = errors,
                TraceId = context.HttpContext.TraceIdentifier
            };

            context.Result = new BadRequestObjectResult(response);
        }
    }
}
```

**Usage:**
```csharp
[HttpPost]
[ValidateModelState]
public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
{
    // Request is guaranteed to be valid here
}
```

---

### 10.4. Custom Exception Types

```csharp
namespace Listo.Notification.Application.Exceptions;

public class NotificationException : Exception
{
    public string ErrorCode { get; }
    public Dictionary<string, object>? Metadata { get; }

    public NotificationException(string message, string errorCode) 
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public NotificationException(string message, string errorCode, Dictionary<string, object>? metadata) 
        : base(message)
    {
        ErrorCode = errorCode;
        Metadata = metadata;
    }
}

public class ResourceNotFoundException : NotificationException
{
    public ResourceNotFoundException(string resourceType, string resourceId)
        : base($"{resourceType} with ID '{resourceId}' was not found", "RESOURCE_NOT_FOUND")
    {
        Metadata = new Dictionary<string, object>
        {
            ["resourceType"] = resourceType,
            ["resourceId"] = resourceId
        };
    }
}

public class RateLimitExceededException : NotificationException
{
    public RateLimitExceededException(string userId, string channel, int limit, int windowSeconds)
        : base($"Rate limit exceeded for user {userId} on {channel} channel", "RATE_LIMIT_EXCEEDED")
    {
        Metadata = new Dictionary<string, object>
        {
            ["userId"] = userId,
            ["channel"] = channel,
            ["limit"] = limit,
            ["windowSeconds"] = windowSeconds,
            ["retryAfter"] = DateTime.UtcNow.AddSeconds(windowSeconds)
        };
    }
}

public class BudgetExceededException : NotificationException
{
    public BudgetExceededException(string serviceOrigin, string channel, decimal currentCost, decimal budget)
        : base($"Budget exceeded for {serviceOrigin} service on {channel} channel", "BUDGET_EXCEEDED")
    {
        Metadata = new Dictionary<string, object>
        {
            ["serviceOrigin"] = serviceOrigin,
            ["channel"] = channel,
            ["currentCost"] = currentCost,
            ["budget"] = budget
        };
    }
}

public class ProviderException : NotificationException
{
    public string Provider { get; }

    public ProviderException(string provider, string message, string errorCode)
        : base($"{provider} error: {message}", errorCode)
    {
        Provider = provider;
        Metadata = new Dictionary<string, object>
        {
            ["provider"] = provider
        };
    }
}
```

---

### 10.5. Enhanced Exception Handling Middleware

```csharp
using System.Net;
using System.Text.Json;
using Listo.Notification.Application.Exceptions;
using Listo.Notification.API.Models.Responses;

namespace Listo.Notification.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unhandled exception occurred. Path: {Path}, TraceId: {TraceId}",
                context.Request.Path,
                context.TraceIdentifier);
            
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message, metadata) = exception switch
        {
            ResourceNotFoundException ex => (
                HttpStatusCode.NotFound,
                ex.ErrorCode,
                ex.Message,
                ex.Metadata
            ),
            RateLimitExceededException ex => (
                HttpStatusCode.TooManyRequests,
                ex.ErrorCode,
                ex.Message,
                ex.Metadata
            ),
            BudgetExceededException ex => (
                HttpStatusCode.PaymentRequired,
                ex.ErrorCode,
                ex.Message,
                ex.Metadata
            ),
            ProviderException ex => (
                HttpStatusCode.BadGateway,
                ex.ErrorCode,
                ex.Message,
                ex.Metadata
            ),
            NotificationException ex => (
                HttpStatusCode.BadRequest,
                ex.ErrorCode,
                ex.Message,
                ex.Metadata
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                "You are not authorized to perform this action",
                null
            ),
            ArgumentException ex => (
                HttpStatusCode.BadRequest,
                "INVALID_ARGUMENT",
                ex.Message,
                null
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "INTERNAL_SERVER_ERROR",
                _environment.IsDevelopment() 
                    ? exception.Message 
                    : "An unexpected error occurred. Please try again later.",
                null
            )
        };

        var response = new ApiErrorResponse
        {
            Success = false,
            Error = errorCode,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        // Add metadata if available
        if (metadata != null && _environment.IsDevelopment())
        {
            response.ValidationErrors = metadata.ToDictionary(
                kvp => kvp.Key,
                kvp => new[] { kvp.Value.ToString() ?? string.Empty }
            );
        }

        // Add stack trace in development
        if (_environment.IsDevelopment())
        {
            response.ValidationErrors ??= new Dictionary<string, string[]>();
            response.ValidationErrors["stackTrace"] = new[] { exception.StackTrace ?? "N/A" };
        }

        // Set retry-after header for rate limit errors
        if (exception is RateLimitExceededException rateLimitEx && 
            rateLimitEx.Metadata?.ContainsKey("retryAfter") == true)
        {
            var retryAfter = (DateTime)rateLimitEx.Metadata["retryAfter"];
            var retryAfterSeconds = (int)(retryAfter - DateTime.UtcNow).TotalSeconds;
            context.Response.Headers.Add("Retry-After", retryAfterSeconds.ToString());
            context.Response.Headers.Add("X-RateLimit-Reset", 
                new DateTimeOffset(retryAfter).ToUnixTimeSeconds().ToString());
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await context.Response.WriteAsync(json);
    }
}
```

---

### 10.6. Result Pattern Implementation

```csharp
namespace Listo.Notification.Application.Common;

public class Result
{
    public bool IsSuccess { get; protected set; }
    public string Message { get; protected set; } = string.Empty;
    public string Error { get; protected set; } = string.Empty;
    public ErrorType ErrorType { get; protected set; }
    public Dictionary<string, string[]>? ValidationErrors { get; protected set; }

    protected Result(bool isSuccess, string message, string error, ErrorType errorType)
    {
        IsSuccess = isSuccess;
        Message = message;
        Error = error;
        ErrorType = errorType;
    }

    public static Result Success(string message = "") =>
        new Result(true, message, string.Empty, ErrorType.None);

    public static Result Failure(string error, string message, ErrorType errorType = ErrorType.Failure) =>
        new Result(false, message, error, errorType);

    public static Result NotFound(string message) =>
        new Result(false, message, "NOT_FOUND", ErrorType.NotFound);

    public static Result Unauthorized(string message) =>
        new Result(false, message, "UNAUTHORIZED", ErrorType.Unauthorized);

    public static Result ValidationError(Dictionary<string, string[]> errors) =>
        new Result(false, "Validation failed", "VALIDATION_ERROR", ErrorType.Validation)
        {
            ValidationErrors = errors
        };
}

public class Result<T> : Result
{
    public T? Value { get; private set; }

    private Result(bool isSuccess, T? value, string message, string error, ErrorType errorType)
        : base(isSuccess, message, error, errorType)
    {
        Value = value;
    }

    public static Result<T> Success(T value, string message = "") =>
        new Result<T>(true, value, message, string.Empty, ErrorType.None);

    public new static Result<T> Failure(string error, string message, ErrorType errorType = ErrorType.Failure) =>
        new Result<T>(false, default, message, error, errorType);

    public new static Result<T> NotFound(string message) =>
        new Result<T>(false, default, message, "NOT_FOUND", ErrorType.NotFound);
}

public enum ErrorType
{
    None,
    Failure,
    NotFound,
    Validation,
    Unauthorized,
    Conflict
}
```

---

### 10.7. Validation Summary

**Validation Layers:**
1. **Request DTOs:** DataAnnotations for basic validation
2. **FluentValidation:** Complex business rules and cross-field validation
3. **Service Layer:** Domain-specific validation (e.g., user exists, template valid)
4. **Database Constraints:** Enforce data integrity at database level

**Error Response Format:**
```json
{
  "success": false,
  "error": "VALIDATION_ERROR",
  "message": "One or more validation errors occurred",
  "validationErrors": {
    "userId": ["UserId is required"],
    "channel": ["Channel must be push, sms, email, or inApp"],
    "templateKey": ["TemplateKey must be lowercase alphanumeric with underscores"]
  },
  "traceId": "00-abc123-def456-00"
}
```

**Rate Limit Error Response:**
```json
{
  "success": false,
  "error": "RATE_LIMIT_EXCEEDED",
  "message": "Rate limit exceeded for user user-123 on push channel",
  "traceId": "00-abc123-def456-00"
}
```

**Response Headers:**
```http
Retry-After: 3600
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1705392000
```

---

## 11. Notification Sending Integrations

This section details the implementation of notification providers for Push, SMS, and Email channels.

### 11.1. Firebase Cloud Messaging (FCM) Integration

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

### 11.2. Twilio SMS Integration

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

### 11.3. SendGrid Email Integration

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

### 11.4. Provider Result Models

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

### 11.5. Retry Logic with Polly

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

## 12. File & Image Upload Handling

This section covers file upload to Azure Blob Storage for in-app messaging attachments.

### 12.1. Azure Blob Storage Configuration

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

### 12.2. Blob Storage Service Implementation

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

### 12.3. File Upload Security Middleware

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

## 13. Testing Strategy

This section covers unit testing, integration testing, and test automation.

### 13.1. Unit Testing Setup

**Installation:**
```bash
dotnet add package xUnit
dotnet add package xUnit.runner.visualstudio
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

### 13.2. Unit Test Examples

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

### 13.3. Integration Testing Setup

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

### 13.4. Test Coverage Configuration

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

## 14. Containerization

This section covers Docker containerization and deployment configuration.

### 14.1. Dockerfile

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

### 14.2. .dockerignore

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

### 14.3. docker-compose.yml (for local development)

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

### 14.4. Environment Configuration

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

## 15. Azure Deployment

This section covers comprehensive Azure deployment configuration using Infrastructure as Code (IaC).

### 15.1. Azure Resources Overview

**Required Resources:**
- Azure Container Registry (ACR)
- Azure Container Apps or AKS
- Azure SQL Database
- Azure Service Bus (Namespace, Queues, Topics)
- Azure SignalR Service
- Azure Key Vault
- Azure Cache for Redis
- Azure Blob Storage
- Azure Application Insights
- Azure Functions App

---

### 15.2. Bicep Infrastructure Configuration

**main.bicep:**
```bicep
@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name')
@allowed([
  'dev'
  'staging'
  'production'
])
param environment string = 'dev'

@description('Unique suffix for resource names')
param resourceSuffix string = uniqueString(resourceGroup().id)

// Container Registry
module containerRegistry 'modules/container-registry.bicep' = {
  name: 'containerRegistryDeployment'
  params: {
    location: location
    acrName: 'listonotification${resourceSuffix}'
    environment: environment
  }
}

// SQL Database
module sqlDatabase 'modules/sql-database.bicep' = {
  name: 'sqlDatabaseDeployment'
  params: {
    location: location
    serverName: 'listo-notification-sql-${environment}'
    databaseName: 'ListoNotification'
    administratorLogin: 'listoAdmin'
    environment: environment
  }
}

// Service Bus
module serviceBus 'modules/service-bus.bicep' = {
  name: 'serviceBusDeployment'
  params: {
    location: location
    namespaceName: 'listo-notification-sb-${environment}'
    environment: environment
  }
}

// SignalR Service
module signalR 'modules/signalr.bicep' = {
  name: 'signalRDeployment'
  params: {
    location: location
    signalRName: 'listo-notification-signalr-${environment}'
    environment: environment
  }
}

// Key Vault
module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVaultDeployment'
  params: {
    location: location
    keyVaultName: 'listo-notif-kv-${resourceSuffix}'
    environment: environment
  }
}

// Redis Cache
module redis 'modules/redis.bicep' = {
  name: 'redisDeployment'
  params: {
    location: location
    redisName: 'listo-notification-redis-${environment}'
    environment: environment
  }
}

// Blob Storage
module storage 'modules/storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
    storageAccountName: 'listonotif${resourceSuffix}'
    containerName: 'message-attachments'
    environment: environment
  }
}

// Application Insights
module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsightsDeployment'
  params: {
    location: location
    appInsightsName: 'listo-notification-insights-${environment}'
    environment: environment
  }
}

// Container App Environment
module containerAppEnv 'modules/container-app-env.bicep' = {
  name: 'containerAppEnvDeployment'
  params: {
    location: location
    environmentName: 'listo-notification-env-${environment}'
    appInsightsId: appInsights.outputs.instrumentationKey
    environment: environment
  }
}

// Container App (API)
module containerApp 'modules/container-app.bicep' = {
  name: 'containerAppDeployment'
  params: {
    location: location
    appName: 'listo-notification-api'
    containerAppEnvId: containerAppEnv.outputs.id
    containerImage: '${containerRegistry.outputs.loginServer}/listo-notification-api:latest'
    environment: environment
    environmentVariables: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: environment
      }
      {
        name: 'ConnectionStrings__DefaultConnection'
        secretRef: 'sql-connection-string'
      }
      {
        name: 'KeyVault__VaultUri'
        value: keyVault.outputs.vaultUri
      }
      {
        name: 'ApplicationInsights__InstrumentationKey'
        value: appInsights.outputs.instrumentationKey
      }
      {
        name: 'Redis__ConnectionString'
        secretRef: 'redis-connection-string'
      }
      {
        name: 'ServiceBus__ConnectionString'
        secretRef: 'servicebus-connection-string'
      }
      {
        name: 'SignalR__ConnectionString'
        secretRef: 'signalr-connection-string'
      }
    ]
  }
}

// Azure Functions
module functions 'modules/functions.bicep' = {
  name: 'functionsDeployment'
  params: {
    location: location
    functionAppName: 'listo-notification-func-${environment}'
    storageAccountName: storage.outputs.storageAccountName
    appInsightsId: appInsights.outputs.id
    environment: environment
  }
}

output containerAppUrl string = containerApp.outputs.fqdn
output keyVaultName string = keyVault.outputs.keyVaultName
output sqlServerFqdn string = sqlDatabase.outputs.serverFqdn
```

---

### 15.3. Container App Module (modules/container-app.bicep)

```bicep
param location string
param appName string
param containerAppEnvId string
param containerImage string
param environment string
param environmentVariables array

resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: appName
  location: location
  tags: {
    environment: environment
    service: 'notification-api'
  }
  properties: {
    managedEnvironmentId: containerAppEnvId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: 'listonotificationacr.azurecr.io'
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'sql-connection-string'
          keyVaultUrl: 'https://listo-notif-kv.vault.azure.net/secrets/sql-connection-string'
          identity: 'system'
        }
        {
          name: 'redis-connection-string'
          keyVaultUrl: 'https://listo-notif-kv.vault.azure.net/secrets/redis-connection-string'
          identity: 'system'
        }
        {
          name: 'servicebus-connection-string'
          keyVaultUrl: 'https://listo-notif-kv.vault.azure.net/secrets/servicebus-connection-string'
          identity: 'system'
        }
        {
          name: 'signalr-connection-string'
          keyVaultUrl: 'https://listo-notif-kv.vault.azure.net/secrets/signalr-connection-string'
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: containerImage
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          probes: [
            {
              type: 'liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 30
              periodSeconds: 10
            }
            {
              type: 'readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 5
            }
          ]
          env: environmentVariables
        }
      ]
      scale: {
        minReplicas: environment == 'production' ? 3 : 1
        maxReplicas: environment == 'production' ? 10 : 3
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output id string = containerApp.id
```

---

### 15.4. Deployment Script

**deploy.sh:**
```bash
#!/bin/bash
set -e

# Variables
RESOURCE_GROUP="listo-notification-rg-${ENVIRONMENT}"
LOCATION="eastus"
ACR_NAME="listonotificationacr"
IMAGE_NAME="listo-notification-api"
IMAGE_TAG="${GITHUB_SHA:-latest}"

# Login to Azure
az login --service-principal \
  --username "$AZURE_CLIENT_ID" \
  --password "$AZURE_CLIENT_SECRET" \
  --tenant "$AZURE_TENANT_ID"

# Create resource group if it doesn't exist
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"

# Deploy infrastructure
echo "Deploying infrastructure..."
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infrastructure/main.bicep \
  --parameters environment="$ENVIRONMENT"

# Build and push Docker image
echo "Building Docker image..."
docker build -t "${ACR_NAME}.azurecr.io/${IMAGE_NAME}:${IMAGE_TAG}" .

echo "Logging into ACR..."
az acr login --name "$ACR_NAME"

echo "Pushing Docker image..."
docker push "${ACR_NAME}.azurecr.io/${IMAGE_NAME}:${IMAGE_TAG}"

# Run database migrations
echo "Running database migrations..."
SQL_CONNECTION_STRING=$(az keyvault secret show \
  --vault-name "listo-notif-kv" \
  --name "sql-connection-string" \
  --query value -o tsv)

dotnet ef database update \
  --connection "$SQL_CONNECTION_STRING" \
  --project Listo.Notification.Infrastructure

# Update container app with new image
echo "Updating container app..."
az containerapp update \
  --name "listo-notification-api" \
  --resource-group "$RESOURCE_GROUP" \
  --image "${ACR_NAME}.azurecr.io/${IMAGE_NAME}:${IMAGE_TAG}"

echo "Deployment complete!"
```

---

### 15.5. Health Check Configuration

**Health Check Endpoint Implementation:**
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Listo.Notification.API.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        NotificationDbContext context,
        ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy(
                "Database is unhealthy",
                ex);
        }
    }
}

public class ServiceBusHealthCheck : IHealthCheck
{
    private readonly IServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusHealthCheck> _logger;

    public ServiceBusHealthCheck(
        IServiceBusClient serviceBusClient,
        ILogger<ServiceBusHealthCheck> logger)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _serviceBusClient.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy("Service Bus is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Bus health check failed");
            return HealthCheckResult.Unhealthy(
                "Service Bus is unhealthy",
                ex);
        }
    }
}

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(
        IConnectionMultiplexer redis,
        ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.PingAsync();
            return HealthCheckResult.Healthy("Redis is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return HealthCheckResult.Unhealthy(
                "Redis is unhealthy",
                ex);
        }
    }
}
```

**Program.cs Health Check Configuration:**
```csharp
// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<ServiceBusHealthCheck>("servicebus", tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

// Map health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});
```

---

## 16. Documentation

This section provides comprehensive API documentation, integration guides, and consumer best practices for the Listo.Notification service.

---

### 16.1. Swagger/OpenAPI Configuration

**Versioned Documentation:**

The Notification API exposes OpenAPI v3 documentation at `/swagger/v1/swagger.json` with an interactive UI at `/swagger`. The API follows semantic versioning with all endpoints prefixed with `/api/v1`.

**Program.cs Configuration:**

```csharp
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Listo.Notification API", 
        Version = "v1",
        Description = "Multi-channel notification and messaging service for the ListoExpress ecosystem",
        Contact = new OpenApiContact
        {
            Name = "ListoExpress Platform Team",
            Email = "platform@listoexpress.com"
        }
    });
    
    // Enable XML comments
    c.EnableAnnotations();
    c.CustomSchemaIds(t => t.FullName);
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // Security definitions
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme 
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Issued by Listo.Auth service."
    });
    
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme 
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key for approved partner integrations or internal service-to-service calls."
    });

    // Global security requirements
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
        },
        {
            new OpenApiSecurityScheme 
            { 
                Reference = new OpenApiReference 
                { 
                    Type = ReferenceType.SecurityScheme, 
                    Id = "ApiKey" 
                } 
            }, 
            Array.Empty<string>()
        }
    });

    // Operation filters for required headers
    c.OperationFilter<CorrelationAndIdempotencyHeadersOperationFilter>();
    c.OperationFilter<RateLimitHeadersOperationFilter>();
    c.OperationFilter<TenantIdHeaderOperationFilter>();
    
    // Example filters (optional)
    c.ExampleFilters();
});

// Register example providers
builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

// Configure Swagger UI
builder.Services.AddSwaggerGenNewtonsoftSupport();

// In app configuration
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Features:EnableSwaggerInProduction"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Listo.Notification API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
        
        // Require authentication in production
        if (!app.Environment.IsDevelopment())
        {
            c.ConfigObject.AdditionalItems.Add("persistAuthorization", "true");
        }
    });
}
```

**Environment-Specific Configuration:**

```json
// appsettings.Development.json
{
  "Features": {
    "EnableSwaggerInProduction": false
  }
}

// appsettings.Production.json (only enable with authentication)
{
  "Features": {
    "EnableSwaggerInProduction": false  // Set to true only for authenticated internal access
  }
}
```

---

### 16.2. API Conventions

**Base Path:**
- All API endpoints are prefixed with `/api/v1`
- Example: `POST /api/v1/notifications`, `GET /api/v1/templates`

**Resource Naming:**
- Use lowercase with hyphen-separated segments
- Examples: `/notification-preferences`, `/batch-operations`, `/cost-tracking`

**JSON Payload:**
- Use camelCase for all property names
- Example: `{ "userId": "123", "templateKey": "order-confirmation", "scheduledAt": "2025-10-20T18:00:00Z" }`

**Standard Headers:**

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization: Bearer <token>` | Yes* | JWT token issued by Listo.Auth |
| `X-Api-Key` | No | Alternative authentication for approved integrations |
| `X-Correlation-Id` | Recommended | Request correlation ID (auto-generated if omitted) |
| `X-Tenant-Id` | Yes** | Tenant identifier for multi-tenancy scoping |
| `Idempotency-Key` | Yes*** | UUID v4 for idempotent POST operations |
| `Accept-Language` | No | Locale preference (e.g., `en-US`, `fr-CA`) |

*Either `Authorization` or `X-Api-Key` required  
**Required unless resolved from JWT `tenantId` claim  
***Required for POST operations that create/send notifications

**Idempotency:**

- **Key Format:** UUID v4 (e.g., `550e8400-e29b-41d4-a716-446655440000`)
- **TTL:** 24 hours
- **Scope:** Per tenant + endpoint + request body hash
- **Behavior:** Replayed requests return the original response with HTTP status 200/201/202
- **Replay Header:** Response includes `Idempotency-Replayed: true`

Example:
```http
POST /api/v1/notifications
Authorization: Bearer eyJ...
X-Tenant-Id: acme-co
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
X-Correlation-Id: trace-abc-123

{ "channel": "email", "templateKey": "order-confirmation", ... }
```

**Pagination:**

- **Strategy:** Cursor-based pagination
- **Default Page Size:** 50 (maximum: 200)
- **Query Parameters:**
  - `pageSize`: Number of results per page (default: 50, max: 200)
  - `cursor`: Base64-encoded cursor for next page

Example:
```http
GET /api/v1/notifications?tenantId=acme-co&pageSize=50&cursor=eyJpZCI6MTIzLCJkYXRlIjoiMjAyNS0wMS0yMCJ9

Response:
{
  "data": [ ... ],
  "pagination": {
    "pageSize": 50,
    "hasMore": true,
    "nextCursor": "eyJpZCI6MTczLCJkYXRlIjoiMjAyNS0wMS0yMSJ9"
  }
}
```

**Error Model:**

All errors follow a consistent JSON structure with appropriate HTTP status codes:

```json
{
  "error": {
    "code": "rate_limit_exceeded",
    "message": "Too many requests. Please retry after the specified time.",
    "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    "timestamp": "2025-10-20T18:00:00Z",
    "details": [
      {
        "field": "to.phoneNumber",
        "issue": "invalid_format",
        "message": "Phone number must be in E.164 format"
      }
    ]
  }
}
```

**Standard HTTP Status Codes:**

- `200 OK`: Successful GET/PUT/PATCH
- `201 Created`: Resource created
- `202 Accepted`: Request accepted for asynchronous processing
- `204 No Content`: Successful DELETE
- `400 Bad Request`: Invalid request payload
- `401 Unauthorized`: Missing or invalid authentication
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Resource not found
- `409 Conflict`: Idempotency key conflict or duplicate resource
- `429 Too Many Requests`: Rate limit exceeded (includes `Retry-After` header)
- `500 Internal Server Error`: Unexpected server error
- `503 Service Unavailable`: Service temporarily unavailable

---

### 16.3. Endpoint Examples

**POST /api/v1/notifications - Send Notification**

Queues a notification for delivery via the specified channel.

```http
POST /api/v1/notifications
Content-Type: application/json
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant-Id: acme-co
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
X-Correlation-Id: order-12345

{
  "channel": "email",
  "templateKey": "order-confirmation",
  "to": {
    "email": "customer@example.com",
    "userId": "user-789"
  },
  "data": {
    "orderId": "ord_12345",
    "orderNumber": "ORD-2025-001",
    "total": 149.99,
    "currency": "USD",
    "items": 3
  },
  "priority": "normal",
  "scheduleAt": null
}
```

**Response (202 Accepted):**
```json
{
  "id": "ntf_abc123def456",
  "status": "queued",
  "channel": "email",
  "scheduledAt": null,
  "createdAt": "2025-10-20T18:00:00Z"
}
```

**GET /api/v1/notifications/{id} - Get Notification Status**

```http
GET /api/v1/notifications/ntf_abc123def456
Authorization: Bearer eyJ...
X-Tenant-Id: acme-co

Response (200 OK):
{
  "id": "ntf_abc123def456",
  "tenantId": "acme-co",
  "channel": "email",
  "status": "delivered",
  "templateKey": "order-confirmation",
  "to": {
    "email": "customer@example.com",
    "userId": "user-789"
  },
  "priority": "normal",
  "createdAt": "2025-10-20T18:00:00Z",
  "sentAt": "2025-10-20T18:00:15Z",
  "deliveredAt": "2025-10-20T18:00:22Z",
  "providerMessageId": "sg_msg_abc123"
}
```

**GET /api/v1/notifications - List Notifications**

```http
GET /api/v1/notifications?tenantId=acme-co&status=delivered&channel=email&pageSize=50
Authorization: Bearer eyJ...

Response (200 OK):
{
  "data": [
    {
      "id": "ntf_abc123",
      "channel": "email",
      "status": "delivered",
      "createdAt": "2025-10-20T18:00:00Z"
    }
  ],
  "pagination": {
    "pageSize": 50,
    "hasMore": false,
    "nextCursor": null
  }
}
```

**POST /api/v1/notification-preferences - Create/Update Preferences**

```http
POST /api/v1/notification-preferences
Authorization: Bearer eyJ...
X-Tenant-Id: acme-co

{
  "userId": "user-789",
  "email": {
    "enabled": true,
    "marketing": false,
    "transactional": true
  },
  "sms": {
    "enabled": true,
    "marketing": false,
    "transactional": true
  },
  "push": {
    "enabled": true
  },
  "quietHours": {
    "enabled": true,
    "start": "22:00",
    "end": "08:00",
    "timezone": "America/New_York"
  },
  "locale": "en-US"
}
```

**POST /api/v1/templates/render - Preview Template (No Send)**

```http
POST /api/v1/templates/render
Authorization: Bearer eyJ...
X-Tenant-Id: acme-co

{
  "templateKey": "order-confirmation",
  "channel": "email",
  "locale": "en-US",
  "data": {
    "orderId": "ord_12345",
    "total": 149.99
  }
}

Response (200 OK):
{
  "subject": "Order Confirmation - ORD-2025-001",
  "body": "<html>...",
  "previewText": "Your order has been confirmed"
}
```

---

### 16.4. Service Integration Guides

**Listo.Auth Integration:**

The Notification service relies on Listo.Auth for JWT-based authentication and authorization.

**Required JWT Scopes:**
- `notifications:send` - Send notifications
- `notifications:read` - Read notification status and history
- `notifications:admin` - Admin operations (budgets, rate limits, templates)

**JWT Claims Used:**
- `sub`: User ID
- `tenantId`: Tenant identifier
- `scope`: Space-separated list of granted scopes
- `role`: User role (customer, driver, support, admin)

**SignalR Authentication:**

For SignalR connections, pass the JWT token via query string:
```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/messaging", {
    accessTokenFactory: () => getAccessToken()
  })
  .build();
```

**Configuration:**

See [`docs/AUTHENTICATION_CONFIGURATION.md`](./docs/AUTHENTICATION_CONFIGURATION.md) for detailed authority, issuer, and clock skew settings.

**Listo.Orders and Listo.RideSharing Integration:**

Two integration patterns are supported:

**1. Direct API Integration (Synchronous)**

For time-sensitive notifications (e.g., driver assignment), services can call the Notification API directly:

```http
POST /api/v1/notifications
X-Service-Secret: {secret_from_keyvault}
X-Tenant-Id: acme-co
Idempotency-Key: orders-driver-assign-{orderId}
X-Correlation-Id: {traceId}

{
  "userId": "driver-456",
  "channel": "push",
  "templateKey": "driver_assignment",
  "priority": "high",
  "synchronous": true,
  "data": {
    "orderId": "ord_12345",
    "customerName": "John Doe",
    "pickupAddress": "123 Main St"
  }
}
```

**2. Asynchronous via Azure Service Bus (Recommended)**

Services publish domain events to Azure Service Bus. The Notification service subscribes and automatically routes notifications based on event mappings.

**Event Mapping Example:**

See [`docs/SERVICE_EVENT_MAPPINGS.md`](./docs/SERVICE_EVENT_MAPPINGS.md) for complete event-to-notification mappings including:
- `com.listo.orders.order-created` → `order_confirmation` template
- `com.listo.ridesharing.driver-assigned` → `driver_assignment` template
- `com.listo.auth.password-reset-requested` → `password_reset` template

---

### 16.5. Azure Service Bus Message Format (CloudEvents 1.0)

**CloudEvents Specification:**

All Service Bus messages MUST use [CloudEvents 1.0](https://github.com/cloudevents/spec/blob/v1.0/spec.md) with JSON content mode.

**Required Attributes:**
- `specversion`: "1.0"
- `id`: Unique event ID (UUID)
- `source`: Event source URI (e.g., "listo.orders/tenants/acme-co")
- `type`: Event type (reverse DNS notation)
- `subject`: Resource subject (e.g., "orders/ord_12345")
- `time`: Event timestamp (ISO 8601)
- `datacontenttype`: "application/json"
- `data`: Event payload

**Example - Order Created Event:**

```json
{
  "specversion": "1.0",
  "id": "evt-550e8400-e29b-41d4-a716-446655440000",
  "source": "listo.orders/tenants/acme-co",
  "type": "com.listo.orders.order-created",
  "subject": "orders/ord_12345",
  "time": "2025-10-20T18:00:00Z",
  "datacontenttype": "application/json",
  "data": {
    "orderId": "ord_12345",
    "userId": "user-789",
    "total": 149.99,
    "currency": "USD",
    "items": [
      {
        "productId": "prod-123",
        "quantity": 2,
        "price": 49.99
      }
    ]
  },
  "extensions": {
    "tenantid": "acme-co",
    "correlationid": "order-trace-12345"
  }
}
```

**Service Bus Mapping:**

- **Message Body:** Complete CloudEvent JSON
- **ApplicationProperties:** Mirror extensions for filtering
  - `tenantid`: "acme-co"
  - `correlationid": "order-trace-12345"
  - `eventtype`: "com.listo.orders.order-created"

**Outgoing Notification Lifecycle Events:**

The Notification service publishes lifecycle events back to Service Bus:

- `com.listo.notification.dispatched` - Notification sent to provider
- `com.listo.notification.delivered` - Confirmed delivery
- `com.listo.notification.failed` - Permanent failure after retries

See [`docs/SERVICE_EVENT_MAPPINGS.md`](./docs/SERVICE_EVENT_MAPPINGS.md) for complete schemas.

---

### 16.6. Rate Limiting and Cost Management (Consumer Guide)

**Per-Tenant Budgets:**

Each tenant has configurable budgets with currency tracking:
- Monthly budget limits per service (Orders, RideSharing, etc.)
- Per-channel quotas (email, SMS, push, in-app)
- Budget alerts at 80% and 100% thresholds

**Default Quotas (Example - Confirm per Tenant):**

| Channel | Daily Limit | Burst Capacity |
|---------|-------------|----------------|
| Email | 10,000 | 200 |
| SMS | 2,000 | 50 |
| Push | 50,000 | 500 |
| In-App | 100,000 | 1,000 |

**Rate Limit Headers:**

All API responses include rate limit information:

```http
HTTP/1.1 200 OK
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 847
X-RateLimit-Reset: 1647878400
X-RateLimit-Policy: tenant-level
```

**Rate Limit Exceeded (429):**

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 60
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1647878400

{
  "error": {
    "code": "rate_limit_exceeded",
    "message": "Rate limit exceeded. Please retry after 60 seconds.",
    "retryAfter": 60
  }
}
```

**OTP SMS Retry Policy Example:**

- Window: 10 minutes
- Max Attempts: 3
- Cooldown: 60 seconds between attempts

**Budget Enforcement:**

- Alerts triggered at 80% and 100% of monthly budget
- After 100%, only high-priority notifications allowed
- Admin override available via `/api/v1/admin/budgets/{tenantId}/override`

For detailed cost management and admin APIs, see [`docs/COST_MANAGEMENT_RATE_LIMITING.md`](./docs/COST_MANAGEMENT_RATE_LIMITING.md).

---

### 16.7. Template Usage and Localization

**Template Resolution Order:**

1. Explicit `templateKey` + `locale` (e.g., `order_confirmation.fr-CA`)
2. `templateKey` + default locale (e.g., `order_confirmation.en`)
3. Global default template

**Placeholder Syntax:**

```handlebars
Hello {{user.firstName}},

Your order #{{order.orderNumber}} totaling {{order.total}} {{order.currency}} has been confirmed.

{{#if order.estimatedDelivery}}
Estimated delivery: {{order.estimatedDelivery}}
{{/if}}
```

**Supported Features:**
- Dot notation for nested properties: `{{order.customer.name}}`
- Default values: `{{user.firstName | "valued customer"}}`
- Conditional blocks: `{{#if condition}}...{{/if}}`
- Loops: `{{#each items}}{{name}}{{/each}}`

**Localization:**

**Locale Resolution:**
1. Explicit `locale` in request payload
2. `Accept-Language` header
3. User preference from database
4. Tenant default locale
5. System default (`en-US`)

**Fallback Chain:**
```
fr-CA → fr → en → system default
```

**HTML Sanitization (Email Templates):**

- Allowlist: `<p>`, `<br>`, `<a>`, `<strong>`, `<em>`, `<ul>`, `<li>`, `<table>`, `<tr>`, `<td>`
- Strip `<script>`, `<iframe>`, `<form>` tags
- Sanitize `href` and `src` attributes (allow https:// only)

**Variable Safety:**

- Null-safe substitution with defaults
- HTML encoding for email content
- Plain text encoding for SMS

---

### 16.8. Webhook Configuration for Providers

**Twilio (SMS) Webhook:**

**Endpoint:** `POST /api/v1/providers/twilio/webhook`

**Signature Validation:**

Twilio signs requests with `X-Twilio-Signature`. Validate using:

```csharp
var validator = new RequestValidator(twilioAuthToken);
var isValid = validator.Validate(
    requestUrl,
    request.Form.ToDictionary(k => k.Key, v => v.Value.ToString()),
    request.Headers["X-Twilio-Signature"]
);
```

**Events Mapped:**
- `queued` → NotificationStatus.Queued
- `sent` → NotificationStatus.Sent
- `delivered` → NotificationStatus.Delivered
- `undelivered` → NotificationStatus.Failed
- `failed` → NotificationStatus.Failed

**Twilio Console Configuration:**
1. Navigate to Phone Numbers → Active Numbers
2. Select your number
3. Under "Messaging", set Webhook URL:
   - `https://notification.listoexpress.com/api/v1/providers/twilio/webhook`
4. HTTP Method: `POST`

---

**SendGrid (Email) Webhook:**

**Endpoint:** `POST /api/v1/providers/sendgrid/webhook`

**Signature Validation:**

Enable signed event webhooks in SendGrid and validate:

```csharp
var publicKey = await keyVaultClient.GetSecretAsync("SendGridWebhookPublicKey");
var signature = request.Headers["X-Twilio-Email-Event-Webhook-Signature"];
var timestamp = request.Headers["X-Twilio-Email-Event-Webhook-Timestamp"];

var verifier = new SignatureVerifier(publicKey);
var isValid = verifier.Verify(
    requestBody,
    signature,
    timestamp
);
```

**Events Mapped:**
- `processed` → NotificationStatus.Sent
- `delivered` → NotificationStatus.Delivered
- `open` → Update analytics (no status change)
- `click` → Update analytics
- `bounce` → NotificationStatus.Failed
- `dropped` → NotificationStatus.Failed
- `spamreport` → NotificationStatus.Failed + block recipient

**SendGrid Configuration:**
1. Settings → Mail Settings → Event Webhook
2. Enable Event Notifications
3. HTTP POST URL: `https://notification.listoexpress.com/api/v1/providers/sendgrid/webhook`
4. Select events: delivered, open, click, bounce, dropped, spamreport
5. Enable Signed Event Webhook

---

**FCM (Push) Webhook:**

**Endpoint:** `POST /api/v1/providers/fcm/webhook`

**Setup:**

FCM doesn't provide native webhooks. Recommended approach:

1. Create Firebase Cloud Function (HTTP trigger)
2. Subscribe to FCM events via Firebase Admin SDK
3. Forward delivery and error events to Notification API

**Cloud Function Example:**

```javascript
const functions = require('firebase-functions');
const admin = require('firebase-admin');
const axios = require('axios');

exports.fcmWebhook = functions.https.onRequest(async (req, res) => {
  const event = req.body;
  
  await axios.post(
    'https://notification.listoexpress.com/api/v1/providers/fcm/webhook',
    event,
    {
      headers: {
        'X-Provider-Secret': process.env.NOTIFICATION_API_SECRET,
        'Content-Type': 'application/json'
      }
    }
  );
  
  res.status(200).send('OK');
});
```

**Signature Validation:**

Validate `X-Provider-Secret` header against shared secret from Key Vault.

**Events Mapped:**
- `token-invalid` → Remove device token
- `canonical-id` → Update device token
- `device-unregistered` → Remove device token

**Security Note:**

Restrict webhook endpoints by IP where possible:
- Twilio IPs: https://www.twilio.com/docs/ips
- SendGrid IPs: https://docs.sendgrid.com/for-developers/tracking-events/getting-started-event-webhook-security#ip-addresses
- FCM: Use VPC and private endpoints or authenticate with shared secrets

Consider exposing webhooks on a separate subdomain (e.g., `webhooks.listoexpress.com`) with dedicated WAF rules.

---

## 17. CI/CD Pipeline

**Note:** CI/CD implementation is deferred per Phase 5 scope. This section outlines the intended pipeline architecture. See Section 20 for detailed stage definitions.

**Preferred Tool:** GitHub Actions

**Intended Pipeline:**
- **Build:** Restore dependencies, compile .NET 9, run dotnet format verification
- **Test:** Unit and integration tests (Section 13 implementation pending)
- **Security:** Static code analysis (SCA), secret scanning, dependency vulnerability checks
- **Package:** Publish artifacts, create container images
- **Deploy:** Deploy to Azure App Service with environment-specific configurations

Implementation will include:
- Multi-environment support (dev, staging, production)
- Manual approval gates for production deployments
- Deployment slots for zero-downtime releases
- Automated rollback on health check failures

---

## 18. Security

This section provides comprehensive security controls, configurations, and best practices for the Listo.Notification service.

---

### 18.1. Security Checklist (Per Environment)

**Identity and Access:**
- ✅ Enforce JWT validation with scopes per endpoint category
- ✅ Implement API key support for trusted partner integrations
- ✅ Configure managed identity for all Azure resource access
- ✅ Restrict service-to-service calls with X-Service-Secret validation
- ✅ Implement least-privilege RBAC for Azure resources

**Data Protection:**
- ✅ Enable TDE (Transparent Data Encryption) on Azure SQL Database
- ✅ Implement app-layer field encryption for PII columns (email, phone, device tokens)
- ✅ Store all secrets in Azure Key Vault (no secrets in code/config)
- ✅ Encrypt data in transit with TLS 1.2+ only
- ✅ Implement secure key rotation procedures (quarterly DEK, annual KEK)

**Network Security:**
- ✅ Enforce HTTPS-only with HSTS headers
- ✅ Configure App Service IP restrictions or VNet integration
- ✅ Use private endpoints for Azure SQL, Service Bus, and Redis
- ✅ Implement NSG rules restricting traffic to required ports only
- ✅ Deploy Azure Front Door or Application Gateway with WAF

**Application Security:**
- ✅ Configure strict CORS with explicit allowed origins
- ✅ Validate all inputs with FluentValidation
- ✅ Sanitize HTML in email templates (allowlist approach)
- ✅ Add security headers (CSP, X-Frame-Options, etc.)
- ✅ Disable detailed error messages in production
- ✅ Mask PII in application logs

**Process and Governance:**
- ✅ Document key rotation runbooks
- ✅ Establish incident response procedures
- ✅ Configure vulnerability scanning in CI/CD
- ✅ Schedule regular dependency updates
- ✅ Conduct quarterly security reviews

---

### 18.2. PII Encryption at Rest

**Envelope Encryption Strategy:**

Use a two-tier key hierarchy for encrypting PII fields (email addresses, phone numbers, device tokens):

1. **Data Encryption Key (DEK):** Per-tenant symmetric key (AES-256-GCM)
2. **Key Encryption Key (KEK):** Azure Key Vault managed key that wraps DEKs

**Storage Schema:**

```sql
CREATE TABLE NotificationQueue (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId NVARCHAR(100) NOT NULL,
    -- Encrypted PII fields
    EncryptedEmail VARBINARY(MAX),
    EmailNonce VARBINARY(12),
    EmailKeyId NVARCHAR(200),
    EncryptedPhoneNumber VARBINARY(MAX),
    PhoneNonce VARBINARY(12),
    PhoneKeyId NVARCHAR(200),
    -- Hashed fields for lookup (HMAC-SHA-256)
    EmailHash AS CONVERT(VARBINARY(32), HASHBYTES('SHA2_256', CONCAT(TenantId, EncryptedEmail))),
    PhoneHash AS CONVERT(VARBINARY(32), HASHBYTES('SHA2_256', CONCAT(TenantId, EncryptedPhoneNumber))),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_NotificationQueue_EmailHash ON NotificationQueue(TenantId, EmailHash);
CREATE INDEX IX_NotificationQueue_PhoneHash ON NotificationQueue(TenantId, PhoneHash);
```

**Service Interface:**

```csharp
public interface IFieldEncryptionService
{
    Task<(byte[] ciphertext, byte[] nonce, string keyId)> EncryptAsync(
        string plaintext, 
        string tenantId, 
        CancellationToken cancellationToken = default);
    
    Task<string> DecryptAsync(
        byte[] ciphertext, 
        byte[] nonce, 
        string keyId, 
        string tenantId, 
        CancellationToken cancellationToken = default);
    
    Task<byte[]> ComputeHashAsync(
        string plaintext, 
        string tenantId, 
        CancellationToken cancellationToken = default);
}
```

**Implementation Pattern:**

```csharp
public class FieldEncryptionService : IFieldEncryptionService
{
    private readonly IKeyVaultClient _keyVault;
    private readonly ILogger<FieldEncryptionService> _logger;
    private readonly ConcurrentDictionary<string, byte[]> _dekCache = new();

    public async Task<(byte[] ciphertext, byte[] nonce, string keyId)> EncryptAsync(
        string plaintext, 
        string tenantId, 
        CancellationToken cancellationToken = default)
    {
        // Get or create tenant DEK
        var dek = await GetOrCreateDekAsync(tenantId, cancellationToken);
        var keyId = $"tenant-{tenantId}-dek-v1";
        
        // Generate random nonce
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);
        
        // Encrypt with AES-GCM
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        
        using var aes = new AesGcm(dek);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        
        // Combine ciphertext + tag
        var result = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);
        
        return (result, nonce, keyId);
    }
    
    private async Task<byte[]> GetOrCreateDekAsync(string tenantId, CancellationToken cancellationToken)
    {
        var cacheKey = $"dek-{tenantId}";
        
        if (_dekCache.TryGetValue(cacheKey, out var cachedDek))
            return cachedDek;
        
        // Load wrapped DEK from Key Vault
        var wrappedDek = await _keyVault.GetSecretAsync($"tenant-{tenantId}-dek", cancellationToken);
        
        if (wrappedDek == null)
        {
            // Generate new DEK and wrap with KEK
            var newDek = new byte[32]; // 256 bits
            RandomNumberGenerator.Fill(newDek);
            
            var kekResult = await _keyVault.WrapKeyAsync("notif-kek", "RSA-OAEP-256", newDek, cancellationToken);
            await _keyVault.SetSecretAsync($"tenant-{tenantId}-dek", Convert.ToBase64String(kekResult), cancellationToken);
            
            _dekCache.TryAdd(cacheKey, newDek);
            return newDek;
        }
        
        // Unwrap DEK using KEK
        var wrappedBytes = Convert.FromBase64String(wrappedDek);
        var unwrappedDek = await _keyVault.UnwrapKeyAsync("notif-kek", "RSA-OAEP-256", wrappedBytes, cancellationToken);
        
        _dekCache.TryAdd(cacheKey, unwrappedDek);
        return unwrappedDek;
    }
}
```

**Lookup Support:**

For finding records by encrypted PII, store deterministic hashes:

```csharp
public async Task<byte[]> ComputeHashAsync(string plaintext, string tenantId, CancellationToken cancellationToken = default)
{
    // Use tenant-specific salt from Key Vault
    var salt = await GetTenantSaltAsync(tenantId, cancellationToken);
    
    using var hmac = new HMACSHA256(salt);
    var input = Encoding.UTF8.GetBytes($"{tenantId}:{plaintext}");
    return hmac.ComputeHash(input);
}
```

---

### 18.3. Azure Key Vault Integration

**Setup with Managed Identity:**

```bash
# Create managed identity
az identity create \
  --name notif-api-identity \
  --resource-group rg-listo-notification-prod

# Create Key Vault
az keyvault create \
  --name kv-listo-notif-prod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --enable-rbac-authorization true

# Grant identity access to Key Vault
az role assignment create \
  --role "Key Vault Crypto User" \
  --assignee <identity-principal-id> \
  --scope /subscriptions/<sub-id>/resourceGroups/rg-listo-notification-prod/providers/Microsoft.KeyVault/vaults/kv-listo-notif-prod

az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee <identity-principal-id> \
  --scope /subscriptions/<sub-id>/resourceGroups/rg-listo-notification-prod/providers/Microsoft.KeyVault/vaults/kv-listo-notif-prod

# Create KEK
az keyvault key create \
  --name notif-kek \
  --vault-name kv-listo-notif-prod \
  --protection software \
  --kty RSA \
  --size 2048
```

**App Settings with Key Vault References (Bicep):**

```bicep
resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-listo-notification-prod'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    siteConfig: {
      appSettings: [
        {
          name: 'KeyVault__VaultUri'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'Twilio__AccountSid'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/TwilioAccountSid/)'
        }
        {
          name: 'Twilio__AuthToken'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/TwilioAuthToken/)'
        }
        {
          name: 'SendGrid__ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/SendGridApiKey/)'
        }
        {
          name: 'ConnectionStrings__NotificationDb'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/SqlConnectionString/)'
        }
      ]
    }
  }
}
```

---

### 18.4. Key Rotation Procedures

**DEK Rotation (Quarterly):**

```csharp
public class DekRotationFunction
{
    [Function("DekRotation")]
    public async Task RunAsync(
        [TimerTrigger("0 0 2 1 */3 *")] TimerInfo timer,  // 2 AM on 1st of every quarter
        FunctionContext context)
    {
        var logger = context.GetLogger<DekRotationFunction>();
        
        logger.LogInformation("Starting quarterly DEK rotation");
        
        var tenants = await _dbContext.Set<TenantEntity>().ToListAsync();
        
        foreach (var tenant in tenants)
        {
            try
            {
                // Generate new DEK
                var newDek = new byte[32];
                RandomNumberGenerator.Fill(newDek);
                
                // Wrap with KEK and store
                var wrappedDek = await _keyVault.WrapKeyAsync("notif-kek", "RSA-OAEP-256", newDek);
                await _keyVault.SetSecretAsync(
                    $"tenant-{tenant.Id}-dek-v{tenant.DekVersion + 1}",
                    Convert.ToBase64String(wrappedDek));
                
                // Update tenant DEK version
                tenant.DekVersion++;
                tenant.DekRotatedAt = DateTime.UtcNow;
                
                // Option 1: Rewrap only (fast - no re-encryption needed)
                // Option 2: Re-encrypt all PII (slow but more secure)
                
                await _dbContext.SaveChangesAsync();
                
                logger.LogInformation("DEK rotated for tenant {TenantId}", tenant.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to rotate DEK for tenant {TenantId}", tenant.Id);
            }
            
            // Throttle to avoid overloading Key Vault
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        
        logger.LogInformation("DEK rotation complete");
    }
}
```

**KEK Rotation (Annual):**

```bash
# Create new KEK version
az keyvault key rotate \
  --name notif-kek \
  --vault-name kv-listo-notif-prod

# Verify new version
az keyvault key show \
  --name notif-kek \
  --vault-name kv-listo-notif-prod \
  --query key.kid

# Rewrap all DEKs with new KEK version (automated function)
# Old KEK version remains available for 90 days for rollback
```

**Secret Rotation Runbook:**

1. **Pre-rotation:**
   - Notify team via Slack/Teams
   - Take snapshot backup of Key Vault
   - Verify monitoring alerts are active

2. **Rotation:**
   - Generate new credentials with provider (Twilio/SendGrid)
   - Update Key Vault secrets with new values
   - Test connectivity with new credentials

3. **Post-rotation:**
   - Monitor error rates for 24 hours
   - Verify no authentication failures
   - Deactivate old credentials after validation period
   - Update audit log

---

### 18.5. JWT Validation and Claims-Based Authorization

**Program.cs Configuration:**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"]; // Listo.Auth URL
        options.Audience = "listo.notification.api";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        
        // Allow SignalR to pass token via query string
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
            },
            OnAuthenticationFailed = context =>
            {
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Add("Token-Expired", "true");
                }
                return Task.CompletedTask;
            }
        };
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    // Scope-based policies
    options.AddPolicy("notifications:send", policy =>
        policy.RequireClaim("scope", "notifications:send"));
    
    options.AddPolicy("notifications:read", policy =>
        policy.RequireClaim("scope", "notifications:read"));
    
    options.AddPolicy("notifications:admin", policy =>
        policy.RequireClaim("scope", "notifications:admin"));
    
    // Role-based policies
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("admin"));
    
    options.AddPolicy("SupportOrAdmin", policy =>
        policy.RequireRole("support", "admin"));
    
    // Combined policies
    options.AddPolicy("ManageTemplates", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context =>
            context.User.HasClaim("scope", "notifications:admin") ||
            context.User.IsInRole("admin"));
    });
});
```

**Custom API Key Authentication Handler:**

```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyValidator _apiKeyValidator;
    
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }
        
        var apiKey = apiKeyHeaderValues.FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("Invalid API key");
        }
        
        var validationResult = await _apiKeyValidator.ValidateAsync(apiKey);
        if (!validationResult.IsValid)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }
        
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, validationResult.ClientId),
            new Claim("tenantId", validationResult.TenantId),
            new Claim("scope", string.Join(" ", validationResult.Scopes))
        };
        
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        
        return AuthenticateResult.Success(ticket);
    }
}
```

---

### 18.6. Network Security

**App Service Configuration:**

```bash
# Enforce HTTPS only
az webapp update \
  --resource-group rg-listo-notification-prod \
  --name app-listo-notification-api-prod \
  --set httpsOnly=true

# Set minimum TLS version
az webapp config set \
  --resource-group rg-listo-notification-prod \
  --name app-listo-notification-api-prod \
  --min-tls-version 1.2

# Configure IP restrictions (if not using VNet)
az webapp config access-restriction add \
  --resource-group rg-listo-notification-prod \
  --name app-listo-notification-api-prod \
  --rule-name AllowFrontDoor \
  --priority 100 \
  --action Allow \
  --service-tag AzureFrontDoor.Backend
```

**Azure SQL Firewall:**

```bash
# Restrict to Azure services only
az sql server firewall-rule create \
  --resource-group rg-listo-notification-prod \
  --server sql-listo-notification-prod \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# For VNet integration, use private endpoint instead
az network private-endpoint create \
  --name pe-sql-notification \
  --resource-group rg-listo-notification-prod \
  --vnet-name vnet-listo-prod \
  --subnet subnet-data \
  --private-connection-resource-id <sql-server-resource-id> \
  --group-id sqlServer \
  --connection-name sql-private-connection
```

**Service Bus and Redis:**

```bash
# Disable public network access
az servicebus namespace update \
  --resource-group rg-listo-notification-prod \
  --name sb-listo-notification-prod \
  --public-network-access Disabled

az redis update \
  --resource-group rg-listo-notification-prod \
  --name redis-listo-notification-prod \
  --public-network-access Disabled

# Create private endpoints
az network private-endpoint create \
  --name pe-servicebus-notification \
  --resource-group rg-listo-notification-prod \
  --vnet-name vnet-listo-prod \
  --subnet subnet-integration \
  --private-connection-resource-id <servicebus-resource-id> \
  --group-id namespace \
  --connection-name sb-private-connection
```

---

### 18.7. CORS Configuration

**Program.cs:**

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", builder =>
    {
        var allowedOrigins = configuration["Cors:AllowedOrigins"]?.Split(',') 
            ?? Array.Empty<string>();
        
        builder.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .WithExposedHeaders(
                "X-RateLimit-Limit",
                "X-RateLimit-Remaining",
                "X-RateLimit-Reset",
                "Retry-After");
    });
});

// Apply CORS before authentication
app.UseCors("FrontendCors");
app.UseAuthentication();
app.UseAuthorization();
```

**appsettings.json:**

```json
{
  "Cors": {
    "AllowedOrigins": "https://app.listoexpress.com,https://admin.listoexpress.com,https://*.listoexpress.com"
  }
}
```

---

### 18.8. HTTPS/TLS and HSTS

**Program.cs:**

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
```

**HSTS Configuration:**

```csharp
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});
```

---

### 18.9. Input Validation and Sanitization

**FluentValidation Example:**

```csharp
public class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.Channel)
            .IsInEnum()
            .WithMessage("Invalid notification channel");
        
        RuleFor(x => x.To.Email)
            .EmailAddress()
            .When(x => x.Channel == NotificationChannel.Email)
            .WithMessage("Invalid email format");
        
        RuleFor(x => x.To.PhoneNumber)
            .Matches(@"^\+[1-9]\d{1,14}$")  // E.164 format
            .When(x => x.Channel == NotificationChannel.Sms)
            .WithMessage("Phone number must be in E.164 format (e.g., +12025551234)");
        
        RuleFor(x => x.Data)
            .Must(data => data?.ToString().Length <= 50000)
            .WithMessage("Template data exceeds maximum size of 50KB");
    }
}
```

**HTML Sanitization for Email Templates:**

```csharp
public class HtmlSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "a", "strong", "em", "b", "i", "u", 
        "ul", "ol", "li", "table", "tr", "td", "th", "thead", "tbody",
        "h1", "h2", "h3", "h4", "h5", "h6", "span", "div"
    };
    
    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "style", "class", "id", "target", "rel"
    };
    
    public string Sanitize(string html)
    {
        var sanitizer = new HtmlSanitizer(AllowedTags, AllowedAttributes);
        
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Remove("http");
        
        return sanitizer.Sanitize(html);
    }
}
```

---

### 18.10. Secrets Management Best Practices

✅ **Never commit secrets to source control**
✅ **Use Azure Key Vault for all secrets**
✅ **Use managed identity for Key Vault access**
✅ **Rotate provider credentials quarterly**
✅ **Use separate credentials per environment**
✅ **Audit Key Vault access logs regularly**
✅ **Implement break-glass procedures for emergency access**

---

### 18.11. Security Headers Middleware

```csharp
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        
        // Prevent MIME sniffing
        headers["X-Content-Type-Options"] = "nosniff";
        
        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";
        
        // Control referrer information
        headers["Referrer-Policy"] = "no-referrer";
        
        // Restrict feature permissions
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        
        // XSS protection (legacy but harmless)
        headers["X-XSS-Protection"] = "0";  // Modern browsers use CSP instead
        
        // Content Security Policy
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self' data:";
        }
        else
        {
            headers["Content-Security-Policy"] = 
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self'; " +
                "img-src 'self'; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'";
        }
        
        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<SecurityHeadersMiddleware>();
```

---

## 19. Maintenance & Future Enhancements

This section defines operational maintenance schedules, performance optimization strategies, and the long-term feature roadmap.

---

### 19.1. Maintenance Schedule

**Daily Operations:**
- 🔍 Review Azure Monitor alerts for 5xx error rates, high response times, queue backlogs
- 🔍 Check Service Bus dead-letter queues; triage and replay failed messages
- 🔍 Inspect Redis memory usage, eviction counts, and connection stability
- 🔍 Monitor provider webhook failure rates (Twilio, SendGrid, FCM)
- 🔍 Verify budget consumption and rate limit hits per tenant

**Weekly Operations:**
- 🔍 Review rate-limit exception patterns and adjust quotas if needed
- 🔍 Analyze webhook delivery failures and update IP allowlists
- 🔍 Audit security logs for authentication failures, suspicious API keys, or unusual access patterns
- 🔍 Check Azure SQL DTU/vCore usage and query performance insights
- 🔍 Review Application Insights failures and performance degradations

**Monthly Operations:**
- 🔧 SQL database maintenance:
  - Rebuild fragmented indexes (>30% fragmentation)
  - Update statistics with full scan
  - Review slow queries and optimize
- 💰 Cost and usage review:
  - Analyze monthly Azure spend by resource
  - Review per-tenant notification costs
  - Adjust quotas and budgets based on trends
- 📦 Dependency updates:
  - Review NuGet package vulnerabilities
  - Update non-breaking dependencies
  - Test breaking updates in staging

**Quarterly Operations:**
- 🔐 DEK rotation per tenant (automated via Azure Function)
- 🔐 Review Key Vault KEK versions and access logs
- 🧪 Load testing and failover drills:
  - Simulate provider outages
  - Test auto-scaling rules
  - Validate circuit breaker behavior
- 🔄 Restore exercises:
  - Test database backups
  - Validate disaster recovery procedures

**Annual Operations:**
- 🔐 KEK rotation in Azure Key Vault
- 🚨 Full disaster recovery simulation
- 📋 Security audit and penetration testing
- 📊 Architecture review and capacity planning

---

### 19.2. Performance Monitoring and Optimization

**Key Metrics:**

| Metric | Target | Alert Threshold |
|--------|--------|----------------|
| API P95 response time | < 500ms | > 1000ms |
| Service Bus queue length | < 1000 | > 5000 |
| SMS delivery time (P95) | < 10s | > 30s |
| Email delivery time (P95) | < 30s | > 60s |
| Push delivery time (P95) | < 5s | > 15s |
| Rate limit allow/deny ratio | > 95% allow | < 90% allow |
| Redis cache hit ratio | > 90% | < 80% |
| App Service CPU | < 70% | > 85% |
| SQL DTU utilization | < 80% | > 90% |

**Optimization Strategies:**

1. **Use async I/O everywhere** - Avoid blocking threads
2. **Batch provider calls** - SendGrid supports up to 1000 recipients per request
3. **Implement circuit breakers** - Prevent cascading failures with Polly
4. **Cache template renders** - Store compiled templates in Redis with 1-hour TTL
5. **Warm-up instances** - Preload DEKs and templates on app start
6. **Use compiled queries** - EF Core compiled queries for frequent lookups
7. **Partition large tables** - Consider table partitioning by date for Notifications table

**Database Tuning:**

```sql
-- Identify missing indexes
SELECT 
    CONVERT(decimal(18,2), user_seeks * avg_total_user_cost * (avg_user_impact * 0.01)) AS index_advantage,
    migs.last_user_seek,
    mid.statement AS table_name,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns
FROM sys.dm_db_missing_index_group_stats AS migs
INNER JOIN sys.dm_db_missing_index_groups AS mig ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details AS mid ON mig.index_handle = mid.index_handle
ORDER BY index_advantage DESC;

-- Identify expensive queries
SELECT TOP 10
    qs.total_worker_time / qs.execution_count AS avg_cpu_time,
    qs.total_elapsed_time / qs.execution_count AS avg_elapsed_time,
    qs.execution_count,
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1, 
        ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text)
        ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) + 1) AS query_text
FROM sys.dm_exec_query_stats AS qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
ORDER BY avg_cpu_time DESC;
```

---

### 19.3. Database Maintenance

**Index Maintenance Script:**

```sql
-- Rebuild fragmented indexes
DECLARE @TableName NVARCHAR(255);
DECLARE @IndexName NVARCHAR(255);
DECLARE @Fragmentation FLOAT;

DECLARE index_cursor CURSOR FOR
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent AS Fragmentation
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS ips
INNER JOIN sys.indexes AS i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 30
    AND ips.page_count > 1000
    AND i.name IS NOT NULL;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @TableName, @IndexName, @Fragmentation;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF @Fragmentation > 30
    BEGIN
        EXEC('ALTER INDEX ' + @IndexName + ' ON ' + @TableName + ' REORGANIZE');
        PRINT 'Reorganized: ' + @TableName + '.' + @IndexName;
    END
    
    FETCH NEXT FROM index_cursor INTO @TableName, @IndexName, @Fragmentation;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;

-- Update statistics
EXEC sp_updatestats;
```

**Automated Maintenance (Azure SQL):**

Enable automatic tuning:

```sql
ALTER DATABASE SCOPED CONFIGURATION SET AUTOMATIC_TUNING (FORCE_LAST_GOOD_PLAN = ON);
ALTER DATABASE SCOPED CONFIGURATION SET AUTOMATIC_TUNING (CREATE_INDEX = ON);
ALTER DATABASE SCOPED CONFIGURATION SET AUTOMATIC_TUNING (DROP_INDEX = ON);
```

---

### 19.4. Backup and Disaster Recovery

**Azure SQL Automated Backups:**

- **Full backups:** Weekly
- **Differential backups:** Every 12-24 hours
- **Transaction log backups:** Every 5-10 minutes
- **Point-in-time restore:** Up to 35 days (configurable)

**Long-Term Retention (LTR):**

```bash
# Configure LTR policy
az sql db ltr-policy set \
  --resource-group rg-listo-notification-prod \
  --server sql-listo-notification-prod \
  --database listonotification \
  --weekly-retention P4W \
  --monthly-retention P12M \
  --yearly-retention P5Y \
  --week-of-year 1
```

**Multi-Region Failover:**

```bash
# Create failover group
az sql failover-group create \
  --name fg-listo-notification \
  --resource-group rg-listo-notification-prod \
  --server sql-listo-notification-prod \
  --partner-server sql-listo-notification-secondary \
  --partner-resource-group rg-listo-notification-secondary \
  --failover-policy Automatic \
  --grace-period 1
```

**RTO/RPO Targets:**

- **Recovery Time Objective (RTO):** 60 minutes
- **Recovery Point Objective (RPO):** 15 minutes
- **Availability SLA:** 99.9% (3 nines)

**Disaster Recovery Runbook:**

1. **Detection:** Monitoring alerts trigger on-call engineer
2. **Assessment:** Determine scope and impact
3. **Decision:** Execute failover if RTO will be exceeded
4. **Failover:**
   ```bash
   az sql failover-group set-primary \
     --name fg-listo-notification \
     --resource-group rg-listo-notification-prod \
     --server sql-listo-notification-secondary
   ```
5. **Verification:** Run smoke tests, check health endpoints
6. **Communication:** Update status page, notify stakeholders
7. **Post-mortem:** Document incident and improvements

---

### 19.5. Scaling Strategies

**App Service (API) Horizontal Scaling:**

```bash
# Configure autoscale rules
az monitor autoscale create \
  --resource-group rg-listo-notification-prod \
  --name autoscale-notification-api \
  --resource app-listo-notification-api-prod \
  --resource-type Microsoft.Web/serverfarms \
  --min-count 3 \
  --max-count 10 \
  --count 3

# Scale out on CPU
az monitor autoscale rule create \
  --resource-group rg-listo-notification-prod \
  --autoscale-name autoscale-notification-api \
  --condition "Percentage CPU > 75 avg 5m" \
  --scale out 2

# Scale in on CPU
az monitor autoscale rule create \
  --resource-group rg-listo-notification-prod \
  --autoscale-name autoscale-notification-api \
  --condition "Percentage CPU < 40 avg 10m" \
  --scale in 1
```

**Azure Functions Scaling:**

- **Consumption Plan:** Automatic scaling (0 to 200 instances)
- **Premium Plan:** Pre-warmed instances, faster cold starts, VNet integration
- **Recommendation:** Use Premium Plan for production workloads

**Azure SQL Scaling:**

```bash
# Scale up compute
az sql db update \
  --resource-group rg-listo-notification-prod \
  --server sql-listo-notification-prod \
  --name listonotification \
  --service-objective S3  # 100 DTUs

# Or switch to vCore model
az sql db update \
  --resource-group rg-listo-notification-prod \
  --server sql-listo-notification-prod \
  --name listonotification \
  --edition GeneralPurpose \
  --family Gen5 \
  --capacity 4  # 4 vCores
```

**Redis Scaling:**

```bash
# Scale up tier
az redis update \
  --resource-group rg-listo-notification-prod \
  --name redis-listo-notification-prod \
  --sku Standard \
  --vm-size C2
```

---

### 19.6. Cost Optimization

**Strategies:**

1. **Right-size SKUs:** Monitor utilization and downscale underutilized resources
2. **Reserved instances:** Purchase 1-year or 3-year reservations for predictable workloads (save up to 72%)
3. **Spot instances:** Use for non-critical background jobs (save up to 90%)
4. **Provider failover:** Only use secondary providers on failure to minimize costs
5. **Template caching:** Cache rendered templates to reduce database hits
6. **Azure Budgets:** Set monthly budgets with alerts at 80% and 100%
7. **Batched operations:** Use SendGrid batch API (up to 1000 recipients) to reduce API calls
8. **Data lifecycle:** Implement auto-archiving and retention policies

**Cost Monitoring:**

```bash
# Create budget
az consumption budget create \
  --budget-name budget-listo-notification \
  --category cost \
  --amount 5000 \
  --time-grain monthly \
  --start-date 2025-01-01 \
  --end-date 2026-12-31 \
  --resource-group rg-listo-notification-prod

# Add alert
az consumption budget create-notification \
  --budget-name budget-listo-notification \
  --notification-key alert-80-percent \
  --enabled true \
  --operator GreaterThan \
  --threshold 80 \
  --contact-emails platform-team@listoexpress.com
```

---

### 19.7. Future Feature Roadmap

**Q2 2025:**
- ✨ Template versioning with A/B testing support
- ✨ WhatsApp Business API integration
- ✨ In-app notification inbox with read receipts

**Q3 2025:**
- ✨ RCS (Rich Communication Services) channel
- ✨ Tenant self-service budget and quota management UI
- ✨ Advanced analytics dashboard with cohort analysis

**Q4 2025:**
- ✨ Multi-region active-active deployment
- ✨ Provider-agnostic abstraction layer with weighted routing
- ✨ Outbox pattern for transactional consistency

**2026:**
- ✨ Full CloudEvents 1.0 adoption across all producers
- ✨ Shared schema registry for event definitions
- ✨ Machine learning-based optimal send time predictions
- ✨ Web-based template editor with live preview

---

### 19.8. Deprecation and Versioning Strategy

**API Versioning:**
- Use URL path versioning: `/api/v1`, `/api/v2`
- Maintain at least 2 major versions simultaneously
- Support older versions for minimum 12 months after new version release

**Deprecation Process:**

1. **Announcement (T-12 months):**
   - Add `Deprecation` header: `Deprecation: true`
   - Add `Sunset` header: `Sunset: Wed, 11 Nov 2026 11:11:11 GMT`
   - Update API documentation with migration guides
   - Email all API consumers

2. **Warning Period (T-6 months):**
   - Log deprecation warnings in Application Insights
   - Track usage of deprecated endpoints
   - Reach out to high-volume consumers

3. **Grace Period (T-3 months):**
   - Add more aggressive warnings
   - Consider rate limiting deprecated endpoints

4. **Removal (T-0):**
   - Remove deprecated endpoints
   - Return 410 Gone for removed endpoints
   - Maintain redirects where possible

---

---

## 20. CI/CD Pipeline

> **Note:** This section complements Section 17 (Azure Deployment) and provides a detailed conceptual outline of the CI/CD pipeline. Implementation is deferred per Phase 5 scope. The preferred tool is **GitHub Actions**.

---

### 20.1. Pipeline Overview

The CI/CD pipeline for Listo.Notification follows a **multi-stage approach** with environment-specific deployments:

**Environments:**
- **Development (dev):** Automatic deployment on merge to `develop` branch
- **Staging (staging):** Automatic deployment on merge to `main` branch
- **Production (prod):** Manual approval required after staging validation

**Pipeline Stages:**

```yaml
Stages:
  1. Build
  2. Test
  3. Security Scan
  4. Package
  5. Deploy (Dev)
  6. Deploy (Staging)
  7. Deploy (Production) [Manual Approval]
```

---

### 20.2. Build Stage

**Objectives:**
- Restore NuGet dependencies
- Build .NET 9 solution
- Validate code formatting
- Generate build artifacts

**Example GitHub Actions workflow:**

```yaml
name: Build and Test

on:
  push:
    branches: [ develop, main ]
  pull_request:
    branches: [ develop, main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Check code formatting
        run: dotnet format --verify-no-changes --verbosity diagnostic
      
      - name: Build
        run: dotnet build --no-restore --configuration Release
      
      - name: Upload build artifacts
        uses: actions/upload-artifact@v4
        with:
          name: build-artifacts
          path: '**/bin/Release/**'
```

---

### 20.3. Test Stage

**Objectives:**
- Run unit tests
- Run integration tests (with Testcontainers)
- Generate code coverage reports
- Enforce minimum coverage thresholds

> **Note:** Detailed testing strategy is outlined in Section 13. Implementation is pending.

**Example test execution:**

```yaml
  test:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run unit tests
        run: |
          dotnet test tests/Listo.Notification.Domain.Tests \
            --configuration Release \
            --no-build \
            --logger trx \
            --collect:"XPlat Code Coverage"
      
      - name: Run integration tests
        run: |
          dotnet test tests/Listo.Notification.Integration.Tests \
            --configuration Release \
            --no-build \
            --logger trx \
            --collect:"XPlat Code Coverage"
      
      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          files: '**/coverage.cobertura.xml'
          fail_ci_if_error: true
```

**Coverage Requirements:**
- **Minimum coverage:** 80% overall
- **Critical paths:** 95% coverage (authentication, payment, PII handling)

---

### 20.4. Security Scan Stage

**Objectives:**
- Software Composition Analysis (SCA) for vulnerable dependencies
- Secret scanning to prevent credential leaks
- Static Application Security Testing (SAST)

**Tools:**

1. **Dependency Scanning:**
   - GitHub Dependabot (automatic PR creation)
   - `dotnet list package --vulnerable`

2. **Secret Scanning:**
   - GitHub Secret Scanning (automatic)
   - TruffleHog or GitGuardian (optional)

3. **SAST:**
   - SonarCloud or CodeQL

**Example workflow:**

```yaml
  security:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Run vulnerability scan
        run: |
          dotnet list package --vulnerable --include-transitive 2>&1 | tee scan.txt
          if grep -q "has the following vulnerable packages" scan.txt; then
            echo "Vulnerable packages found!"
            exit 1
          fi
      
      - name: Initialize CodeQL
        uses: github/codeql-action/init@v3
        with:
          languages: csharp
      
      - name: Autobuild
        uses: github/codeql-action/autobuild@v3
      
      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v3
```

---

### 20.5. Package Stage

**Objectives:**
- Publish .NET 9 application
- Create Docker image
- Push image to Azure Container Registry (ACR)
- Generate release artifacts

**Example workflow:**

```yaml
  package:
    needs: [test, security]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Publish application
        run: |
          dotnet publish src/Listo.Notification.Api/Listo.Notification.Api.csproj \
            --configuration Release \
            --output ./publish \
            --no-restore
      
      - name: Build Docker image
        run: |
          docker build -t listonotification:${{ github.sha }} .
      
      - name: Login to Azure Container Registry
        uses: azure/docker-login@v1
        with:
          login-server: ${{ secrets.ACR_LOGIN_SERVER }}
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}
      
      - name: Push to ACR
        run: |
          docker tag listonotification:${{ github.sha }} \
            ${{ secrets.ACR_LOGIN_SERVER }}/listonotification:${{ github.sha }}
          docker push ${{ secrets.ACR_LOGIN_SERVER }}/listonotification:${{ github.sha }}
          docker tag listonotification:${{ github.sha }} \
            ${{ secrets.ACR_LOGIN_SERVER }}/listonotification:latest
          docker push ${{ secrets.ACR_LOGIN_SERVER }}/listonotification:latest
```

---

### 20.6. Deploy Stage

**Objectives:**
- Deploy to Azure App Service using slot swaps
- Run smoke tests
- Require manual approval for production

**Deployment Strategy:**
- **Blue-Green Deployment:** Use App Service deployment slots
- **Canary Deployment:** Route 10% traffic to new slot, monitor, then swap

**Example deployment workflow:**

```yaml
  deploy-dev:
    needs: package
    runs-on: ubuntu-latest
    environment: development
    steps:
      - name: Login to Azure
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS_DEV }}
      
      - name: Deploy to App Service (Dev)
        uses: azure/webapps-deploy@v2
        with:
          app-name: app-listo-notification-dev
          images: ${{ secrets.ACR_LOGIN_SERVER }}/listonotification:${{ github.sha }}
      
      - name: Run smoke tests
        run: |
          curl -f https://app-listo-notification-dev.azurewebsites.net/health || exit 1

  deploy-staging:
    needs: deploy-dev
    runs-on: ubuntu-latest
    environment: staging
    steps:
      - name: Login to Azure
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS_STAGING }}
      
      - name: Deploy to staging slot
        uses: azure/webapps-deploy@v2
        with:
          app-name: app-listo-notification-prod
          slot-name: staging
          images: ${{ secrets.ACR_LOGIN_SERVER }}/listonotification:${{ github.sha }}
      
      - name: Run smoke tests on staging
        run: |
          curl -f https://app-listo-notification-prod-staging.azurewebsites.net/health || exit 1

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: 
      name: production
      url: https://api.listo.com/notifications
    steps:
      - name: Login to Azure
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS_PROD }}
      
      - name: Swap staging to production
        run: |
          az webapp deployment slot swap \
            --resource-group rg-listo-notification-prod \
            --name app-listo-notification-prod \
            --slot staging \
            --target-slot production
      
      - name: Verify production health
        run: |
          sleep 30
          curl -f https://api.listo.com/notifications/health || exit 1
```

**Manual Approval:**
- GitHub Environments with required reviewers
- Approval required before production deployment
- Notifications sent to on-call engineer

---

### 20.7. Rollback Strategy

In case of deployment failure or production issues:

**Immediate Rollback (< 5 minutes):**

```bash
# Swap back to previous slot
az webapp deployment slot swap \
  --resource-group rg-listo-notification-prod \
  --name app-listo-notification-prod \
  --slot production \
  --target-slot staging
```

**Version Rollback (re-deploy previous image):**

```bash
# Deploy previous known-good image
az webapp config container set \
  --resource-group rg-listo-notification-prod \
  --name app-listo-notification-prod \
  --docker-custom-image-name $ACR_LOGIN_SERVER/listonotification:previous-sha
```

---

### 20.8. Pipeline Monitoring

**Key Metrics:**
- Build success rate
- Average build duration
- Test pass rate
- Deployment frequency
- Mean time to recovery (MTTR)
- Change failure rate

**Alerts:**
- Failed builds trigger Slack/Teams notification
- Failed deployments page on-call engineer
- Security scan failures block pipeline progression

---

## 21. Security (Runtime Security, Monitoring, and Incident Response)

> **Note:** This section complements Section 18 (Security Design) and focuses on **runtime security operations**, monitoring, threat detection, and incident response.

---

### 21.1. Runtime Security Monitoring

**Azure Defender Integration:**

Enable Microsoft Defender for Cloud across all resources:

```bash
# Enable Defender for App Service
az security pricing create \
  --name AppServices \
  --tier Standard

# Enable Defender for SQL
az security pricing create \
  --name SqlServers \
  --tier Standard

# Enable Defender for Storage
az security pricing create \
  --name StorageAccounts \
  --tier Standard

# Enable Defender for Key Vault
az security pricing create \
  --name KeyVaults \
  --tier Standard
```

**Application Insights Security Monitoring:**

1. **PII Masking with Custom Telemetry Processor:**

```csharp
public class PiiMaskingTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;
    private readonly Regex _emailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
    private readonly Regex _phoneRegex = new Regex(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b");
    private readonly Regex _tokenRegex = new Regex(@"\b[A-Za-z0-9_-]{20,}\b");

    public PiiMaskingTelemetryProcessor(ITelemetryProcessor next)
    {
        _next = next;
    }

    public void Process(ITelemetry item)
    {
        // Mask PII in trace messages
        if (item is TraceTelemetry trace)
        {
            trace.Message = MaskPii(trace.Message);
        }

        // Mask PII in exception messages
        if (item is ExceptionTelemetry exception)
        {
            exception.Message = MaskPii(exception.Message);
            if (exception.Exception != null)
            {
                // Consider masking stack trace carefully to preserve debugging info
            }
        }

        // Mask PII in custom properties
        if (item is ISupportProperties propItem)
        {
            foreach (var key in propItem.Properties.Keys.ToList())
            {
                propItem.Properties[key] = MaskPii(propItem.Properties[key]);
            }
        }

        _next.Process(item);
    }

    private string MaskPii(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        input = _emailRegex.Replace(input, "***EMAIL***");
        input = _phoneRegex.Replace(input, "***PHONE***");
        input = _tokenRegex.Replace(input, "***TOKEN***");

        return input;
    }
}

// Register in Program.cs
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

builder.Services.AddApplicationInsightsTelemetryProcessor<PiiMaskingTelemetryProcessor>();
```

2. **Anomaly Detection Alerts:**

Configure alerts in Application Insights:

```bash
# Alert on 5xx spike
az monitor metrics alert create \
  --name alert-5xx-spike \
  --resource-group rg-listo-notification-prod \
  --scopes /subscriptions/{sub}/resourceGroups/rg-listo-notification-prod/providers/Microsoft.Web/sites/app-listo-notification-prod \
  --condition "count requests/server where resultCode >= 500 > 50" \
  --window-size 5m \
  --evaluation-frequency 1m

# Alert on 401/403 spike (potential brute-force)
az monitor metrics alert create \
  --name alert-auth-failures \
  --resource-group rg-listo-notification-prod \
  --scopes /subscriptions/{sub}/resourceGroups/rg-listo-notification-prod/providers/Microsoft.Web/sites/app-listo-notification-prod \
  --condition "count requests/server where resultCode in (401, 403) > 100" \
  --window-size 5m \
  --evaluation-frequency 1m

# Alert on 429 rate-limit spike
az monitor metrics alert create \
  --name alert-rate-limit-spike \
  --resource-group rg-listo-notification-prod \
  --scopes /subscriptions/{sub}/resourceGroups/rg-listo-notification-prod/providers/Microsoft.Web/sites/app-listo-notification-prod \
  --condition "count requests/server where resultCode == 429 > 500" \
  --window-size 5m \
  --evaluation-frequency 1m
```

**Microsoft Sentinel Integration (Optional):**

For advanced threat detection:

1. **Ingest logs from:**
   - App Service (application logs, HTTP logs)
   - Azure SQL (audit logs, threat detection)
   - Azure AD (sign-in logs, audit logs)
   - Key Vault (access logs)

2. **Analytics rules:**
   - Brute-force detection (multiple 401s from same IP)
   - Token abuse (same token used from multiple IPs)
   - Unusual IP addresses (geo-location anomalies)
   - Privilege escalation attempts
   - Mass data export attempts

---

### 21.2. DDoS Protection and WAF

**Azure Front Door with WAF:**

```bash
# Create WAF policy
az network front-door waf-policy create \
  --name waf-listo-notification \
  --resource-group rg-listo-notification-prod \
  --mode Prevention

# Enable managed rules
az network front-door waf-policy managed-rules add \
  --policy-name waf-listo-notification \
  --resource-group rg-listo-notification-prod \
  --type Microsoft_DefaultRuleSet \
  --version 2.1

# Add custom rule for rate limiting at edge
az network front-door waf-policy rule create \
  --policy-name waf-listo-notification \
  --resource-group rg-listo-notification-prod \
  --name RateLimitPerIP \
  --rule-type RateLimitRule \
  --rate-limit-threshold 1000 \
  --rate-limit-duration 1 \
  --match-variable RemoteAddr \
  --action Block
```

**Geo-Filtering for Admin Endpoints:**

```bash
# Block admin endpoints for non-allowed countries
az network front-door waf-policy rule create \
  --policy-name waf-listo-notification \
  --resource-group rg-listo-notification-prod \
  --name BlockAdminNonUS \
  --rule-type MatchRule \
  --match-variable RequestUri \
  --operator Contains \
  --match-value "/api/v1/admin" \
  --transforms Lowercase \
  --action Block \
  --match-condition "RemoteAddr GeoMatch Not US,CA,GB"
```

**IP Allowlisting for Provider Webhooks:**

```bash
# Restrict Twilio webhook endpoint to Twilio IPs
az webapp config access-restriction add \
  --resource-group rg-listo-notification-prod \
  --name app-listo-notification-prod \
  --rule-name AllowTwilioWebhooks \
  --action Allow \
  --ip-address 54.252.254.64/26 \
  --priority 100 \
  --scm-site false

# Add additional Twilio IP ranges
# See: https://www.twilio.com/docs/usage/webhooks/ip-addresses
```

---

### 21.3. Runtime Hardening and Secrets Rotation Alarms

**Key Vault Secret Expiry Alerts:**

```bash
# Alert 30 days before expiry
az monitor metrics alert create \
  --name alert-kv-secret-expiry-30d \
  --resource-group rg-listo-notification-prod \
  --scopes /subscriptions/{sub}/resourceGroups/rg-listo-notification-prod/providers/Microsoft.KeyVault/vaults/kv-listo-notification \
  --condition "avg DaysToSecretExpiry < 30" \
  --window-size 1d \
  --evaluation-frequency 1d

# Alert 7 days before expiry
az monitor metrics alert create \
  --name alert-kv-secret-expiry-7d \
  --resource-group rg-listo-notification-prod \
  --scopes /subscriptions/{sub}/resourceGroups/rg-listo-notification-prod/providers/Microsoft.KeyVault/vaults/kv-listo-notification \
  --condition "avg DaysToSecretExpiry < 7" \
  --window-size 1d \
  --evaluation-frequency 6h

# Alert on failed decrypt operations (key mismatch)
az monitor metrics alert create \
  --name alert-kv-failed-decrypts \
  --resource-group rg-listo-notification-prod \
  --scopes /subscriptions/{sub}/resourceGroups/rg-listo-notification-prod/providers/Microsoft.KeyVault/vaults/kv-listo-notification \
  --condition "count ServiceApiResult where ActivityName == 'Decrypt' and ResultType == 'Failed' > 10" \
  --window-size 5m \
  --evaluation-frequency 1m
```

**Lock Down Debug and Swagger in Production:**

```csharp
// Program.cs
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}
else
{
    // Optional: Allow Swagger in production behind authentication
    var swaggerEnabled = builder.Configuration.GetValue<bool>("Swagger:EnableInProduction");
    if (swaggerEnabled)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Listo.Notification API v1");
            options.RoutePrefix = "swagger";
        });
        
        // Require authentication for Swagger UI
        app.MapGet("/swagger/{**path}", async context =>
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
            await context.Response.WriteAsync("Redirect to Swagger UI");
        });
    }
    
    app.UseExceptionHandler("/error");
    app.UseHsts();
}
```

---

### 21.4. Incident Response Runbooks

**Credential Leak:**

1. **Immediate Actions (< 15 minutes):**
   ```bash
   # Revoke provider API keys
   # Twilio: https://console.twilio.com/
   # SendGrid: https://app.sendgrid.com/settings/api_keys
   # FCM: https://console.firebase.google.com/
   
   # Rotate Key Vault secrets
   az keyvault secret set \
     --vault-name kv-listo-notification \
     --name TwilioApiKey \
     --value "new-secure-key"
   
   # Invalidate all API keys (if leaked)
   # Execute emergency disable via admin endpoint or direct DB update
   ```

2. **Investigation (< 1 hour):**
   - Review audit logs to identify scope of exposure
   - Check Application Insights for unauthorized API calls
   - Review Azure AD sign-in logs for suspicious activity

3. **Communication (< 2 hours):**
   - Notify security team and stakeholders
   - Prepare incident report
   - Update status page if user-facing impact

4. **Remediation (< 24 hours):**
   - Complete key rotation across all environments
   - Review and enhance secret scanning in CI/CD
   - Update access policies and audit retention
   - Conduct post-mortem

**Suspicious Token Usage:**

1. **Detection Indicators:**
   - Same JWT used from multiple IPs in different geo-locations
   - High-frequency API calls beyond normal user patterns
   - Access to unusual endpoints or data scopes

2. **Response Actions:**
   ```bash
   # Revoke specific client application (if token source identified)
   # Update Azure AD app registration to revoke tokens
   
   # Block tenant temporarily (admin API)
   curl -X POST https://api.listo.com/notifications/api/v1/admin/tenants/{tenantId}/block \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "X-Reason: Suspicious activity detected"
   
   # Force re-authentication for affected users
   az ad user revoke-sign-in-sessions --id user@example.com
   ```

3. **Audit Actions:**
   - Export all API calls from suspicious token via Application Insights
   - Review data access and potential PII exposure
   - Notify affected tenants if data breach confirmed

**Webhook Abuse:**

1. **Detection Indicators:**
   - High volume of webhook calls from unexpected IPs
   - Invalid signatures or malformed payloads
   - Attempts to inject malicious content

2. **Response Actions:**
   ```bash
   # Rotate webhook shared secrets
   az keyvault secret set \
     --vault-name kv-listo-notification \
     --name TwilioWebhookSecret \
     --value "new-secret"
   
   # Update Twilio/SendGrid webhook configuration with new secret
   # Add IP restrictions if possible
   
   az webapp config access-restriction add \
     --resource-group rg-listo-notification-prod \
     --name app-listo-notification-prod \
     --rule-name BlockSuspiciousIP \
     --action Deny \
     --ip-address 1.2.3.4/32 \
     --priority 50
   
   # Temporarily disable webhook endpoint (feature flag)
   az webapp config appsettings set \
     --resource-group rg-listo-notification-prod \
     --name app-listo-notification-prod \
     --settings WebhooksEnabled=false
   ```

**Outbound Provider Outage:**

1. **Detection:**
   - High failure rate for specific channel (SMS, Email, Push)
   - Increased response times from provider APIs
   - Circuit breaker trips

2. **Response Actions:**
   ```csharp
   // Activate failover provider (already implemented in SendingService)
   // Manual override via admin endpoint if automated failover insufficient
   
   // Temporarily disable affected channel or route traffic
   curl -X POST https://api.listo.com/notifications/api/v1/admin/providers/twilio/disable \
     -H "Authorization: Bearer $ADMIN_TOKEN"
   
   // Increase retry backoff and reduce concurrent sends
   curl -X PATCH https://api.listo.com/notifications/api/v1/admin/config/retry-policy \
     -H "Authorization: Bearer $ADMIN_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{ "baseDelaySeconds": 10, "backoffFactor": 3, "maxAttempts": 3 }'
   ```

3. **Communication:**
   - Raise Customer Advisory on status page
   - Update estimated resolution time
   - Notify high-priority customers directly

---

### 21.5. Tenant-Level Kill Switch

**Purpose:** Ability to immediately disable all notification sending for a specific tenant and/or channel in case of abuse, budget overrun, or security incident.

**Implementation:**

```csharp
// Domain entity
public class TenantConfiguration
{
    public string TenantId { get; set; }
    public bool IsEnabled { get; set; }
    public Dictionary<string, bool> ChannelEnabled { get; set; } // channel => enabled
    public string DisabledReason { get; set; }
    public DateTime? DisabledAt { get; set; }
    public string DisabledBy { get; set; }
}

// Admin endpoint
[HttpPost("api/v1/admin/tenants/{tenantId}/disable")]
[Authorize(Policy = "notifications:admin")]
public async Task<IActionResult> DisableTenant(
    string tenantId,
    [FromBody] DisableTenantRequest request)
{
    var config = await _configService.GetTenantConfigAsync(tenantId);
    
    config.IsEnabled = false;
    config.DisabledReason = request.Reason;
    config.DisabledAt = DateTime.UtcNow;
    config.DisabledBy = User.Identity.Name;
    
    await _configService.UpdateTenantConfigAsync(config);
    
    // Invalidate cache
    await _cacheService.RemoveAsync($"tenant-config:{tenantId}");
    
    // Audit log
    _logger.LogWarning(
        "Tenant {TenantId} disabled by {Admin}. Reason: {Reason}",
        tenantId, User.Identity.Name, request.Reason);
    
    return Ok(new { message = "Tenant disabled successfully" });
}

// Rate limiter integration
public async Task<bool> IsAllowedAsync(string tenantId, string channel)
{
    // Check kill switch first (cached)
    var config = await _cacheService.GetOrSetAsync(
        $"tenant-config:{tenantId}",
        async () => await _configService.GetTenantConfigAsync(tenantId),
        TimeSpan.FromMinutes(5));
    
    if (!config.IsEnabled) return false;
    if (!config.ChannelEnabled.GetValueOrDefault(channel, true)) return false;
    
    // Continue with normal rate limiting
    return await _rateLimiter.AllowAsync(tenantId, channel);
}
```

**Audit Trail:**

All kill switch actions logged to immutable audit table:

```sql
CREATE TABLE AuditLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TenantId NVARCHAR(100) NOT NULL,
    Action NVARCHAR(50) NOT NULL, -- 'TenantDisabled', 'TenantEnabled', 'ChannelDisabled'
    PerformedBy NVARCHAR(200) NOT NULL,
    Reason NVARCHAR(500) NULL,
    Details NVARCHAR(MAX) NULL, -- JSON payload
    IpAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    INDEX IX_AuditLog_TenantId_Timestamp (TenantId, Timestamp DESC)
);
```

---

### 21.6. Forensics and Evidence Preservation

**Security Audit Log Retention:**

- **Default retention:** 365 days (configurable per compliance requirements)
- **Storage:** Azure SQL with automated backups + immutable blob storage for critical events

**Immutable Storage Setup:**

```bash
# Create storage account with immutability support
az storage account create \
  --name stlistoauditprod \
  --resource-group rg-listo-notification-prod \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2

# Create container with immutability policy
az storage container create \
  --name security-audit-logs \
  --account-name stlistoauditprod \
  --public-access off

az storage container immutability-policy create \
  --account-name stlistoauditprod \
  --container-name security-audit-logs \
  --period 365 \
  --allow-protected-append-writes false
```

**Critical Events for Immutable Storage:**

1. Admin actions (tenant disable, quota changes, key rotation)
2. Authentication failures (401/403) with details
3. PII access events
4. Configuration changes to security settings
5. Provider credential usage

**Export to Immutable Storage:**

```csharp
public class AuditLogExporter : BackgroundService
{
    private readonly IAuditLogRepository _auditRepo;
    private readonly BlobServiceClient _blobClient;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Export critical audit logs hourly
            var logs = await _auditRepo.GetCriticalLogsAsync(DateTime.UtcNow.AddHours(-1));
            
            if (logs.Any())
            {
                var containerClient = _blobClient.GetBlobContainerClient("security-audit-logs");
                var blobName = $"audit-{DateTime.UtcNow:yyyyMMdd-HH}.jsonl";
                var blobClient = containerClient.GetAppendBlobClient(blobName);
                
                // Create if not exists (immutable policy prevents deletion)
                await blobClient.CreateIfNotExistsAsync();
                
                // Append logs (JSON Lines format)
                using var stream = new MemoryStream();
                using var writer = new StreamWriter(stream);
                foreach (var log in logs)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(log));
                }
                await writer.FlushAsync();
                stream.Position = 0;
                
                await blobClient.AppendBlockAsync(stream);
            }
            
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
```

---

## 22. GDPR & Compliance

This section details data protection requirements, user rights, and compliance procedures for GDPR and other privacy regulations.

---

### 22.1. Data Retention Policies

**Default Retention Periods (Configurable per Tenant):**

| Data Type | Default Retention | Purge Mechanism |
|-----------|-------------------|------------------|
| Notifications (content) | 90 days | Azure Function (daily) |
| Delivery receipts & provider events | 180 days | Azure Function (daily) |
| Support chat transcripts | 180 days | Azure Function (daily) |
| Customer-Driver chat | 30 days | Azure Function (daily) |
| Audit logs (security) | 365 days | Manual (legal holds) |
| Audit logs (operational) | 90 days | Azure Function (weekly) |
| Queue processing records | 30 days after completion | Azure Function (daily) |
| PII (email, phone, device tokens) | Tied to user account lifecycle | On user deletion request |

**Configuration Model:**

```csharp
public class TenantRetentionPolicy
{
    public string TenantId { get; set; }
    public int NotificationRetentionDays { get; set; } = 90;
    public int DeliveryReceiptRetentionDays { get; set; } = 180;
    public int SupportChatRetentionDays { get; set; } = 180;
    public int CustomerDriverChatRetentionDays { get; set; } = 30;
    public int OperationalAuditLogRetentionDays { get; set; } = 90;
    public bool LegalHoldActive { get; set; } = false;
    public string LegalHoldReason { get; set; }
    public DateTime? LegalHoldStartDate { get; set; }
}
```

**Purge Job Implementation:**

```csharp
// Azure Function - Timer Trigger (daily at 2 AM UTC)
[Function("DataRetentionPurgeJob")]
public async Task Run(
    [TimerTrigger("0 0 2 * * *")] TimerInfo timer,
    FunctionContext context)
{
    var logger = context.GetLogger<DataRetentionPurgeJob>();
    logger.LogInformation("Starting data retention purge job at {Time}", DateTime.UtcNow);
    
    var tenants = await _tenantService.GetAllTenantsAsync();
    
    foreach (var tenant in tenants)
    {
        var policy = await _retentionService.GetPolicyAsync(tenant.TenantId);
        
        // Skip if legal hold active
        if (policy.LegalHoldActive)
        {
            logger.LogWarning("Skipping purge for tenant {TenantId} due to legal hold", tenant.TenantId);
            continue;
        }
        
        // Purge notifications
        var notificationCutoff = DateTime.UtcNow.AddDays(-policy.NotificationRetentionDays);
        var purgedNotifications = await _notificationRepo.PurgeOlderThanAsync(
            tenant.TenantId, notificationCutoff);
        
        // Purge delivery receipts
        var receiptCutoff = DateTime.UtcNow.AddDays(-policy.DeliveryReceiptRetentionDays);
        var purgedReceipts = await _receiptRepo.PurgeOlderThanAsync(
            tenant.TenantId, receiptCutoff);
        
        // Purge chat messages
        var supportChatCutoff = DateTime.UtcNow.AddDays(-policy.SupportChatRetentionDays);
        var purgedSupportChats = await _chatRepo.PurgeSupportChatsOlderThanAsync(
            tenant.TenantId, supportChatCutoff);
        
        var customerDriverChatCutoff = DateTime.UtcNow.AddDays(-policy.CustomerDriverChatRetentionDays);
        var purgedCustomerDriverChats = await _chatRepo.PurgeCustomerDriverChatsOlderThanAsync(
            tenant.TenantId, customerDriverChatCutoff);
        
        logger.LogInformation(
            "Purge complete for tenant {TenantId}. Notifications: {NotifCount}, Receipts: {ReceiptCount}, Support: {SupportCount}, CD: {CDCount}",
            tenant.TenantId, purgedNotifications, purgedReceipts, purgedSupportChats, purgedCustomerDriverChats);
    }
}
```

---

### 22.2. Right to be Forgotten (Erasure)

**Process Overview:**

1. **Input:** User ID and/or PII (email, phone, device token)
2. **Locate Data:** Search across all tables using tenant context and hashed PII indices
3. **Erasure:** Hard-delete or anonymize (retain non-PII analytics where legally permitted)
4. **Tombstone:** Create deletion record to prevent re-creation from caches
5. **Audit:** Log all erasure actions for compliance

**API Endpoint:**

```csharp
[HttpPost("api/v1/admin/users/{userId}/erase")]
[Authorize(Policy = "notifications:admin")]
public async Task<IActionResult> EraseUserData(
    string userId,
    [FromQuery] string tenantId,
    [FromBody] ErasureRequest request)
{
    // Validate request
    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(userId))
        return BadRequest("TenantId and UserId are required");
    
    // Check for legal holds
    var policy = await _retentionService.GetPolicyAsync(tenantId);
    if (policy.LegalHoldActive && !request.OverrideLegalHold)
    {
        return Conflict(new { error = "Legal hold active. Erasure blocked." });
    }
    
    // Execute erasure
    var result = await _erasureService.EraseUserDataAsync(new ErasureContext
    {
        TenantId = tenantId,
        UserId = userId,
        Email = request.Email,
        PhoneNumber = request.PhoneNumber,
        DeviceTokens = request.DeviceTokens,
        RequestedBy = User.Identity.Name,
        Reason = request.Reason
    });
    
    // Audit log
    await _auditService.LogAsync(new AuditEvent
    {
        TenantId = tenantId,
        Action = "UserDataErased",
        PerformedBy = User.Identity.Name,
        Details = JsonSerializer.Serialize(new
        {
            userId,
            recordsErased = result.RecordsErased,
            tablesAffected = result.TablesAffected
        })
    });
    
    return Ok(new
    {
        message = "User data erased successfully",
        recordsErased = result.RecordsErased,
        tablesAffected = result.TablesAffected
    });
}
```

**Erasure Service Implementation:**

```csharp
public class UserDataErasureService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IDeviceRepository _deviceRepo;
    private readonly IPreferencesRepository _preferencesRepo;
    private readonly IChatRepository _chatRepo;
    private readonly ITombstoneRepository _tombstoneRepo;
    private readonly ICacheService _cacheService;
    
    public async Task<ErasureResult> EraseUserDataAsync(ErasureContext context)
    {
        var result = new ErasureResult();
        
        // 1. Locate by UserId
        var notificationIds = await _notificationRepo.FindByUserIdAsync(
            context.TenantId, context.UserId);
        result.RecordsErased += await _notificationRepo.HardDeleteAsync(notificationIds);
        result.TablesAffected.Add("Notifications");
        
        // 2. Locate by encrypted PII (using hashed indices)
        if (!string.IsNullOrEmpty(context.Email))
        {
            var emailHash = ComputeHash(context.Email, context.TenantId);
            var emailNotifications = await _notificationRepo.FindByEmailHashAsync(
                context.TenantId, emailHash);
            result.RecordsErased += await _notificationRepo.HardDeleteAsync(emailNotifications);
        }
        
        if (!string.IsNullOrEmpty(context.PhoneNumber))
        {
            var phoneHash = ComputeHash(context.PhoneNumber, context.TenantId);
            var phoneNotifications = await _notificationRepo.FindByPhoneHashAsync(
                context.TenantId, phoneHash);
            result.RecordsErased += await _notificationRepo.HardDeleteAsync(phoneNotifications);
        }
        
        // 3. Erase device tokens
        if (context.DeviceTokens?.Any() ?? false)
        {
            foreach (var token in context.DeviceTokens)
            {
                var tokenHash = ComputeHash(token, context.TenantId);
                var devices = await _deviceRepo.FindByTokenHashAsync(
                    context.TenantId, tokenHash);
                result.RecordsErased += await _deviceRepo.HardDeleteAsync(devices);
            }
            result.TablesAffected.Add("Devices");
        }
        
        // 4. Erase preferences
        result.RecordsErased += await _preferencesRepo.DeleteByUserIdAsync(
            context.TenantId, context.UserId);
        result.TablesAffected.Add("NotificationPreferences");
        
        // 5. Erase or anonymize chat messages
        // Option A: Hard delete (GDPR requirement)
        result.RecordsErased += await _chatRepo.DeleteByUserIdAsync(
            context.TenantId, context.UserId);
        
        // Option B: Anonymize (retain analytics)
        // await _chatRepo.AnonymizeByUserIdAsync(context.TenantId, context.UserId);
        result.TablesAffected.Add("ChatMessages");
        
        // 6. Create tombstone to prevent cache resurrection
        await _tombstoneRepo.CreateAsync(new Tombstone
        {
            TenantId = context.TenantId,
            UserId = context.UserId,
            ErasedAt = DateTime.UtcNow,
            ErasedBy = context.RequestedBy,
            Reason = context.Reason
        });
        
        // 7. Invalidate caches
        await _cacheService.RemoveAsync($"user:{context.TenantId}:{context.UserId}:*");
        
        return result;
    }
    
    private string ComputeHash(string value, string tenantSalt)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(tenantSalt));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(hashBytes);
    }
}
```

**Tombstone Table:**

```sql
CREATE TABLE Tombstones (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    TenantId NVARCHAR(100) NOT NULL,
    UserId NVARCHAR(100) NOT NULL,
    ErasedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ErasedBy NVARCHAR(200) NOT NULL,
    Reason NVARCHAR(500) NULL,
    INDEX IX_Tombstones_TenantId_UserId (TenantId, UserId)
);
```

---

### 22.3. User Data Export (Subject Access Request)

**Endpoint:**

```csharp
[HttpGet("api/v1/users/{userId}/notifications/export")]
[Authorize(Policy = "notifications:read")]
public async Task<IActionResult> ExportUserData(
    string userId,
    [FromQuery] string tenantId,
    [FromQuery] ExportFormat format = ExportFormat.Json)
{
    // Validate tenant access
    if (!User.HasClaim("tenantId", tenantId) && !User.IsInRole("Admin"))
        return Forbid();
    
    // Check export size
    var recordCount = await _notificationRepo.CountByUserIdAsync(tenantId, userId);
    
    if (recordCount > 10000)
    {
        // Asynchronous export for large datasets
        var exportJobId = await _exportService.QueueExportJobAsync(new ExportRequest
        {
            TenantId = tenantId,
            UserId = userId,
            Format = format,
            RequestedBy = User.Identity.Name
        });
        
        return Accepted(new
        {
            message = "Export job queued. Download link will be sent via email.",
            jobId = exportJobId,
            estimatedCompletionMinutes = Math.Ceiling(recordCount / 1000.0)
        });
    }
    
    // Synchronous export for small datasets
    var data = await _exportService.ExportUserDataAsync(tenantId, userId);
    
    return format switch
    {
        ExportFormat.Json => Ok(data),
        ExportFormat.JsonLines => File(
            Encoding.UTF8.GetBytes(string.Join("\n", data.Select(JsonSerializer.Serialize))),
            "application/x-ndjson",
            $"user-{userId}-export.jsonl"),
        _ => BadRequest("Unsupported format")
    };
}
```

**Asynchronous Export Job:**

```csharp
// Azure Function - Queue Trigger
[Function("DataExportProcessor")]
public async Task Run(
    [QueueTrigger("data-export-queue")] ExportRequest request,
    FunctionContext context)
{
    var logger = context.GetLogger<DataExportProcessor>();
    logger.LogInformation("Processing export for user {UserId} in tenant {TenantId}",
        request.UserId, request.TenantId);
    
    // Export data
    var data = await _exportService.ExportUserDataAsync(request.TenantId, request.UserId);
    
    // Upload to blob storage with SAS token
    var blobClient = _blobServiceClient
        .GetBlobContainerClient("data-exports")
        .GetBlobClient($"{request.TenantId}/{request.UserId}/{Guid.NewGuid()}.json");
    
    await blobClient.UploadAsync(
        new BinaryData(JsonSerializer.Serialize(data)),
        overwrite: false);
    
    // Generate SAS URL (expires in 24 hours)
    var sasUrl = blobClient.GenerateSasUri(
        BlobSasPermissions.Read,
        DateTimeOffset.UtcNow.AddHours(24));
    
    // Send email with download link
    await _emailService.SendAsync(new EmailMessage
    {
        To = request.RequestedBy, // or user email from profile
        Subject = "Your data export is ready",
        TemplateKey = "data-export-ready",
        Data = new
        {
            downloadUrl = sasUrl.ToString(),
            expiresAt = DateTimeOffset.UtcNow.AddHours(24).ToString("yyyy-MM-dd HH:mm UTC")
        }
    });
    
    logger.LogInformation("Export complete for user {UserId}", request.UserId);
}
```

**Export Data Structure:**

```json
{
  "userId": "user-123",
  "tenantId": "acme-co",
  "exportedAt": "2025-10-20T18:00:00Z",
  "notifications": [
    {
      "id": "ntf_abc123",
      "channel": "email",
      "templateKey": "order-confirmation",
      "status": "delivered",
      "sentAt": "2025-10-15T12:00:00Z",
      "deliveredAt": "2025-10-15T12:00:05Z",
      "content": {
        "subject": "Your order #12345 has been confirmed",
        "body": "Thank you for your order..."
      }
    }
  ],
  "preferences": {
    "email": true,
    "sms": false,
    "push": true,
    "inApp": true
  },
  "devices": [
    {
      "platform": "ios",
      "registeredAt": "2025-01-10T10:00:00Z",
      "lastUsedAt": "2025-10-19T15:30:00Z"
    }
  ]
}
```

---

### 22.4. Consent Management

**Preference Model:**

```csharp
public class NotificationPreferences
{
    public string TenantId { get; set; }
    public string UserId { get; set; }
    
    // Per-channel opt-in/out
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    
    // Granular topic preferences
    public Dictionary<string, bool> TopicPreferences { get; set; } = new();
    // Example: { "marketing": false, "transactional": true, "security": true }
    
    // Consent evidence
    public DateTime ConsentGivenAt { get; set; }
    public string ConsentSource { get; set; } // "signup", "settings", "api"
    public string ConsentIpAddress { get; set; }
    public string ConsentUserAgent { get; set; }
    
    public DateTime? LastModifiedAt { get; set; }
}
```

**Consent Validation Before Send:**

```csharp
public async Task<bool> CanSendNotificationAsync(
    string tenantId,
    string userId,
    string channel,
    string topic)
{
    var prefs = await _preferencesRepo.GetAsync(tenantId, userId);
    
    // Check channel preference
    var channelEnabled = channel switch
    {
        "email" => prefs.EmailEnabled,
        "sms" => prefs.SmsEnabled,
        "push" => prefs.PushEnabled,
        "inApp" => prefs.InAppEnabled,
        _ => false
    };
    
    if (!channelEnabled) return false;
    
    // Check topic preference (if specified)
    if (!string.IsNullOrEmpty(topic))
    {
        if (!prefs.TopicPreferences.TryGetValue(topic, out var topicEnabled))
            topicEnabled = true; // Default to enabled for new topics
        
        if (!topicEnabled) return false;
    }
    
    // Override: Always allow critical/regulatory notifications
    var criticalTopics = new[] { "security", "legal", "account" };
    if (criticalTopics.Contains(topic, StringComparer.OrdinalIgnoreCase))
        return true;
    
    return true;
}
```

**Consent Capture:**

```csharp
[HttpPost("api/v1/notification-preferences")]
[Authorize(Policy = "notifications:read")]
public async Task<IActionResult> UpdatePreferences(
    [FromBody] UpdatePreferencesRequest request)
{
    var userId = User.FindFirst("sub")?.Value;
    var tenantId = User.FindFirst("tenantId")?.Value;
    
    var prefs = await _preferencesRepo.GetOrCreateAsync(tenantId, userId);
    
    // Update preferences
    prefs.EmailEnabled = request.EmailEnabled;
    prefs.SmsEnabled = request.SmsEnabled;
    prefs.PushEnabled = request.PushEnabled;
    prefs.InAppEnabled = request.InAppEnabled;
    prefs.TopicPreferences = request.TopicPreferences ?? prefs.TopicPreferences;
    
    // Capture consent evidence
    if (prefs.ConsentGivenAt == default)
    {
        prefs.ConsentGivenAt = DateTime.UtcNow;
        prefs.ConsentSource = "settings";
        prefs.ConsentIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        prefs.ConsentUserAgent = HttpContext.Request.Headers["User-Agent"].ToString();
    }
    
    prefs.LastModifiedAt = DateTime.UtcNow;
    
    await _preferencesRepo.UpdateAsync(prefs);
    
    // Audit log
    _logger.LogInformation(
        "User {UserId} updated notification preferences. Email: {Email}, SMS: {Sms}, Push: {Push}",
        userId, prefs.EmailEnabled, prefs.SmsEnabled, prefs.PushEnabled);
    
    return Ok(prefs);
}
```

---

### 22.5. Audit Logging for Compliance

**Audit Events:**

- Template create/update/delete
- Preference changes (opt-in/out)
- Quota and budget modifications
- Admin actions (tenant disable, key rotation)
- Data export and erasure requests

**Immutable Audit Table:**

```sql
CREATE TABLE ComplianceAuditLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TenantId NVARCHAR(100) NOT NULL,
    UserId NVARCHAR(100) NULL,
    Action NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(50) NULL, -- 'Template', 'Preferences', 'User'
    EntityId NVARCHAR(200) NULL,
    PerformedBy NVARCHAR(200) NOT NULL,
    IpAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    ChangesBefore NVARCHAR(MAX) NULL, -- JSON
    ChangesAfter NVARCHAR(MAX) NULL, -- JSON
    Reason NVARCHAR(500) NULL,
    INDEX IX_ComplianceAuditLog_TenantId_Timestamp (TenantId, Timestamp DESC),
    INDEX IX_ComplianceAuditLog_UserId (UserId),
    INDEX IX_ComplianceAuditLog_Action (Action)
);

-- Prevent modifications (enforce append-only)
CREATE TRIGGER TR_ComplianceAuditLog_PreventUpdate
ON ComplianceAuditLog
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    RAISERROR('ComplianceAuditLog is immutable. Updates and deletes are not allowed.', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO
```

**Export for Compliance:**

```bash
# Export audit logs for specific tenant and time range
az sql db export \
  --resource-group rg-listo-notification-prod \
  --server sql-listo-notification-prod \
  --name listonotification \
  --admin-user sqladmin \
  --admin-password $SQL_PASSWORD \
  --storage-key-type SharedAccessKey \
  --storage-key $STORAGE_KEY \
  --storage-uri "https://stlistoauditprod.blob.core.windows.net/audit-exports/tenant-acme-2025-Q4.bacpac"
```

---

### 22.6. Provider DPAs and Data Processing

**Data Processing Agreements (DPAs):**

- **Twilio:** [https://www.twilio.com/legal/data-protection-addendum](https://www.twilio.com/legal/data-protection-addendum)
- **SendGrid:** [https://www.twilio.com/legal/data-protection-addendum](https://www.twilio.com/legal/data-protection-addendum) (owned by Twilio)
- **Firebase/Google:** [https://firebase.google.com/support/privacy](https://firebase.google.com/support/privacy)

**Processing Roles:**

- **Listo.Notification:** Data Controller (for tenant data)
- **Twilio/SendGrid/Firebase:** Data Processors (sub-processors)

**Minimal PII Sharing:**

- **SMS (Twilio):** Only phone number and message content
- **Email (SendGrid):** Only email address, subject, body, attachments
- **Push (FCM):** Only device token and message payload
- **No unnecessary PII:** Names, addresses, IDs are not shared unless required for delivery

**Transfer Safeguards:**

- **Standard Contractual Clauses (SCCs):** In place for EU/UK data transfers
- **Supplementary Measures:** Encryption in transit (TLS 1.2+), encryption at rest for PII

---

### 22.7. Cross-Border Data Transfer

**Regional Endpoint Alignment:**

- Prefer provider regional endpoints aligned with tenant residency
- Example: Twilio EU region for EU tenants

**Configuration:**

```json
{
  "Providers": {
    "Twilio": {
      "Regions": {
        "EU": {
          "AccountSid": "{{KeyVault:TwilioAccountSidEU}}",
          "AuthToken": "{{KeyVault:TwilioAuthTokenEU}}",
          "ApiBaseUrl": "https://api.twilio.com" // Twilio routes internally
        },
        "US": {
          "AccountSid": "{{KeyVault:TwilioAccountSidUS}}",
          "AuthToken": "{{KeyVault:TwilioAuthTokenUS}}",
          "ApiBaseUrl": "https://api.twilio.com"
        }
      }
    }
  }
}
```

**Avoid Storing Provider Payloads:**

- Do not persist webhook payloads beyond retention window
- Minimize logging of provider responses
- Mask PII in Application Insights (Section 21.1)

---

### 22.8. Privacy by Design and Data Minimization

**Principles:**

1. **Collect Minimum PII:** Only collect email, phone, device token as needed for delivery
2. **Pseudonymization:** Use hashed indices for lookups; store encrypted values
3. **Tokenization Preference:** Where possible, use tokens instead of raw PII
4. **Log Masking:** Never log raw PII in Application Insights or console logs (Section 21.1)
5. **Short-Lived Sessions:** JWT tokens with short expiration (15 minutes access, 7 days refresh)
6. **Secure Deletion:** Hard-delete PII on user request; no soft deletes

**PII Minimization Example:**

```csharp
// Bad: Logging raw PII
_logger.LogInformation("Sending email to {Email}", user.Email);

// Good: Logging masked or hashed PII
_logger.LogInformation("Sending email to user {UserId} (hash: {EmailHash})",
    user.Id, ComputeHash(user.Email));
```

---

## 23. Clean Architecture Implementation

This section describes the folder structure, dependency rules, and architectural patterns used in Listo.Notification, following Clean Architecture principles **without MediatR**.

---

### 23.1. Folder Structure

**Solution Structure:**

```
Listo.Notification/
├── src/
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── Notification.cs
│   │   │   ├── Device.cs
│   │   │   ├── Template.cs
│   │   │   ├── NotificationPreferences.cs
│   │   │   ├── TenantConfiguration.cs
│   │   │   └── ...
│   │   ├── ValueObjects/
│   │   │   ├── EmailAddress.cs
│   │   │   ├── PhoneNumber.cs
│   │   │   ├── DeviceToken.cs
│   │   │   └── ...
│   │   ├── DomainServices/
│   │   │   ├── TemplateRenderer.cs
│   │   │   ├── CostCalculator.cs
│   │   │   └── ...
│   │   ├── DomainEvents/
│   │   │   ├── NotificationSent.cs
│   │   │   ├── NotificationFailed.cs
│   │   │   └── ...
│   │   └── Listo.Notification.Domain.csproj
│   │
│   ├── Application/
│   │   ├── Services/
│   │   │   ├── NotificationService.cs
│   │   │   ├── SendingService.cs
│   │   │   ├── TemplateService.cs
│   │   │   ├── PreferencesService.cs
│   │   │   └── ...
│   │   ├── DTOs/
│   │   │   ├── SendNotificationRequest.cs
│   │   │   ├── NotificationResponse.cs
│   │   │   ├── TemplateDto.cs
│   │   │   └── ...
│   │   ├── Validators/
│   │   │   ├── SendNotificationRequestValidator.cs
│   │   │   └── ...
│   │   ├── Interfaces/ (Ports)
│   │   │   ├── INotificationRepository.cs
│   │   │   ├── IEmailSender.cs
│   │   │   ├── ISmsSender.cs
│   │   │   ├── IPushSender.cs
│   │   │   ├── ICacheService.cs
│   │   │   ├── IRateLimiter.cs
│   │   │   ├── IFieldEncryptionService.cs
│   │   │   ├── IDomainEventDispatcher.cs
│   │   │   └── ...
│   │   └── Listo.Notification.Application.csproj
│   │
│   ├── Infrastructure/
│   │   ├── Data/
│   │   │   ├── NotificationDbContext.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── NotificationRepository.cs
│   │   │   │   ├── DeviceRepository.cs
│   │   │   │   ├── TemplateRepository.cs
│   │   │   │   └── ...
│   │   │   ├── Configurations/
│   │   │   │   ├── NotificationConfiguration.cs
│   │   │   │   └── ...
│   │   │   └── Migrations/
│   │   ├── Providers/
│   │   │   ├── Twilio/
│   │   │   │   ├── TwilioSmsClient.cs
│   │   │   │   └── TwilioWebhookHandler.cs
│   │   │   ├── SendGrid/
│   │   │   │   ├── SendGridEmailClient.cs
│   │   │   │   └── SendGridWebhookHandler.cs
│   │   │   ├── FCM/
│   │   │   │   ├── FcmPushClient.cs
│   │   │   │   └── FcmWebhookHandler.cs
│   │   │   └── ...
│   │   ├── Caching/
│   │   │   ├── RedisCacheService.cs
│   │   │   └── RateLimiter.cs
│   │   ├── Messaging/
│   │   │   ├── ServiceBusPublisher.cs
│   │   │   ├── ServiceBusSubscriber.cs
│   │   │   └── DomainEventDispatcher.cs
│   │   ├── Security/
│   │   │   ├── FieldEncryptionService.cs
│   │   │   └── KeyVaultSecretProvider.cs
│   │   └── Listo.Notification.Infrastructure.csproj
│   │
│   ├── Api/
│   │   ├── Controllers/
│   │   │   ├── NotificationsController.cs
│   │   │   ├── TemplatesController.cs
│   │   │   ├── PreferencesController.cs
│   │   │   ├── AdminController.cs
│   │   │   └── WebhooksController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   ├── SecurityHeadersMiddleware.cs
│   │   │   └── IdempotencyMiddleware.cs
│   │   ├── Hubs/
│   │   │   └── NotificationHub.cs (SignalR)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Listo.Notification.Api.csproj
│   │
│   └── Functions/
│       ├── BackgroundJobs/
│       │   ├── ScheduledNotificationRunner.cs
│       │   ├── RetryProcessor.cs
│       │   ├── DataRetentionPurgeJob.cs
│       │   ├── BudgetCalculator.cs
│       │   └── KeyRotationJob.cs
│       ├── EventHandlers/
│       │   ├── OrderCreatedHandler.cs
│       │   ├── RideCompletedHandler.cs
│       │   └── ...
│       └── Listo.Notification.Functions.csproj
│
├── tests/
│   ├── Listo.Notification.Domain.Tests/
│   ├── Listo.Notification.Application.Tests/
│   ├── Listo.Notification.Infrastructure.Tests/
│   ├── Listo.Notification.Api.Tests/
│   └── Listo.Notification.Integration.Tests/
│
├── docs/
│   ├── DEPLOYMENT_GUIDE.md
│   ├── AUTHENTICATION_CONFIGURATION.md
│   ├── COST_MANAGEMENT_RATE_LIMITING.md
│   └── SERVICE_EVENT_MAPPINGS.md
│
├── Listo.Notification.sln
├── README.md
├── TODO.md
└── NOTIFICATION_MGMT_PLAN.md
```

---

### 23.2. Dependency Rules and Inversion of Control

**Dependency Flow:**

```
Api/Functions → Application → Domain
       ↓
 Infrastructure (implements Application interfaces)
```

**Rules:**

1. **Domain** has NO dependencies (pure business logic)
2. **Application** depends ONLY on Domain
3. **Infrastructure** depends on Application (implements interfaces)
4. **Api/Functions** depend on Application and Infrastructure (for DI registration)

**Inversion of Control:**

```csharp
// Application/Interfaces/IEmailSender.cs (Port)
public interface IEmailSender
{
    Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}

// Infrastructure/Providers/SendGrid/SendGridEmailClient.cs (Adapter)
public class SendGridEmailClient : IEmailSender
{
    private readonly SendGridClient _client;
    
    public async Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct)
    {
        // SendGrid-specific implementation
    }
}

// Api/Program.cs (DI Registration)
builder.Services.AddScoped<IEmailSender, SendGridEmailClient>();
```

**No MediatR:**

Services orchestrate directly for simplicity and performance:

```csharp
// Application/Services/NotificationService.cs
public class NotificationService
{
    private readonly ISendingService _sendingService;
    private readonly INotificationRepository _repo;
    private readonly IDomainEventDispatcher _eventDispatcher;
    
    public async Task<NotificationResponse> SendAsync(SendNotificationRequest request)
    {
        // Validate
        // Create entity
        var notification = Notification.Create(request);
        
        // Persist
        await _repo.AddAsync(notification);
        
        // Send
        var result = await _sendingService.SendAsync(notification);
        
        // Dispatch domain event
        await _eventDispatcher.DispatchAsync(new NotificationSent(notification.Id));
        
        return NotificationResponse.FromEntity(notification);
    }
}
```

---

### 23.3. Mapping Strategy

**DTOs ↔ Entities:**

Prefer explicit mapping for clarity and performance:

```csharp
// Manual mapping
public class NotificationResponse
{
    public string Id { get; set; }
    public string Status { get; set; }
    public DateTime SentAt { get; set; }
    
    public static NotificationResponse FromEntity(Notification entity)
    {
        return new NotificationResponse
        {
            Id = entity.Id,
            Status = entity.Status.ToString(),
            SentAt = entity.SentAt
        };
    }
}
```

**Alternative (Optional):**

Use Mapster for complex mappings:

```csharp
// Configure mappings
public class MappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Notification, NotificationResponse>()
            .Map(dest => dest.Status, src => src.Status.ToString());
    }
}

// Usage
var response = notification.Adapt<NotificationResponse>();
```

---

### 23.4. Interface Segregation

**Narrow Interfaces:**

```csharp
// Good: Specific interfaces per provider type
public interface IEmailSender
{
    Task<SendResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}

public interface ISmsSender
{
    Task<SendResult> SendAsync(SmsMessage message, CancellationToken ct = default);
}

public interface IPushSender
{
    Task<SendResult> SendAsync(PushMessage message, CancellationToken ct = default);
}

// Bad: One large interface
public interface INotificationSender // DON'T DO THIS
{
    Task<SendResult> SendEmailAsync(...);
    Task<SendResult> SendSmsAsync(...);
    Task<SendResult> SendPushAsync(...);
}
```

**Read vs Write Separation:**

```csharp
// Separate read and write operations where beneficial
public interface INotificationReadRepository
{
    Task<Notification> GetByIdAsync(string id);
    Task<IEnumerable<Notification>> FindAsync(Expression<Func<Notification, bool>> predicate);
}

public interface INotificationWriteRepository
{
    Task AddAsync(Notification notification);
    Task UpdateAsync(Notification notification);
    Task DeleteAsync(string id);
}
```

---

### 23.5. Repository Pattern Usage

**Approach:**

- **If using EF Core extensively:** Consider lean repositories or direct DbContext via interface (IAppDbContext) to avoid anemic abstraction
- **For high-throughput read models:** Use Dapper or compiled queries

**Example with IAppDbContext:**

```csharp
// Application/Interfaces/IAppDbContext.cs
public interface IAppDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<Device> Devices { get; }
    DbSet<Template> Templates { get; }
    
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// Infrastructure/Data/NotificationDbContext.cs
public class NotificationDbContext : DbContext, IAppDbContext
{
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<Template> Templates { get; set; }
}

// Application service uses interface directly
public class NotificationService
{
    private readonly IAppDbContext _dbContext;
    
    public async Task<Notification> GetAsync(string id)
    {
        return await _dbContext.Notifications
            .Where(n => n.Id == id)
            .FirstOrDefaultAsync();
    }
}
```

**High-Throughput Reads with Dapper:**

```csharp
public class NotificationQueryRepository
{
    private readonly IDbConnection _connection;
    
    public async Task<IEnumerable<NotificationSummary>> GetRecentAsync(
        string tenantId, int pageSize = 50)
    {
        const string sql = @"
            SELECT TOP (@PageSize)
                Id, Channel, Status, SentAt
            FROM Notifications
            WHERE TenantId = @TenantId
            ORDER BY CreatedAt DESC";
        
        return await _connection.QueryAsync<NotificationSummary>(
            sql, new { TenantId = tenantId, PageSize = pageSize });
    }
}
```

---

### 23.6. Domain Events Without MediatR

**Lightweight Event Dispatcher:**

```csharp
// Application/Interfaces/IDomainEventDispatcher.cs
public interface IDomainEventDispatcher
{
    Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}

// Domain/DomainEvents/IDomainEvent.cs
public interface IDomainEvent
{
    string EventId { get; }
    DateTime OccurredAt { get; }
}

// Domain/DomainEvents/NotificationSent.cs
public record NotificationSent(
    string NotificationId,
    string TenantId,
    string Channel) : IDomainEvent
{
    public string EventId { get; } = Guid.NewGuid().ToString();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

**Implementation Options:**

**Option 1: In-Memory Channel (simple, fast):**

```csharp
// Infrastructure/Messaging/InMemoryEventDispatcher.cs
public class InMemoryEventDispatcher : IDomainEventDispatcher
{
    private readonly Channel<IDomainEvent> _channel;
    
    public InMemoryEventDispatcher()
    {
        _channel = Channel.CreateUnbounded<IDomainEvent>();
        
        // Background processor
        Task.Run(ProcessEventsAsync);
    }
    
    public async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        await _channel.Writer.WriteAsync(domainEvent, ct);
    }
    
    private async Task ProcessEventsAsync()
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync())
        {
            // Route to handlers
            // Example: Log, update analytics, etc.
            Console.WriteLine($"Event: {evt.GetType().Name}");
        }
    }
}
```

**Option 2: Azure Service Bus (cross-service, reliable):**

```csharp
// Infrastructure/Messaging/ServiceBusEventDispatcher.cs
public class ServiceBusEventDispatcher : IDomainEventDispatcher
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    
    public ServiceBusEventDispatcher(ServiceBusClient client)
    {
        _client = client;
        _sender = _client.CreateSender("listo-notifications-events");
    }
    
    public async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        // Convert to CloudEvents format
        var cloudEvent = new CloudEvent(
            source: "listo.notification",
            type: $"com.listo.notification.{domainEvent.GetType().Name.ToLower()}",
            jsonSerializableData: domainEvent)
        {
            Id = domainEvent.EventId,
            Time = domainEvent.OccurredAt
        };
        
        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(cloudEvent));
        await _sender.SendMessageAsync(message, ct);
    }
}
```

**Outbox Pattern (Future Enhancement):**

```csharp
public class OutboxEventDispatcher : IDomainEventDispatcher
{
    private readonly IAppDbContext _dbContext;
    
    public async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        // Store event in outbox table (same transaction as entity changes)
        var outboxEvent = new OutboxEvent
        {
            EventId = domainEvent.EventId,
            EventType = domainEvent.GetType().FullName,
            Payload = JsonSerializer.Serialize(domainEvent),
            OccurredAt = domainEvent.OccurredAt,
            Processed = false
        };
        
        _dbContext.OutboxEvents.Add(outboxEvent);
        await _dbContext.SaveChangesAsync(ct);
    }
}

// Background service processes outbox and publishes to Service Bus
```

---

### 23.7. Testing Approach Per Layer

**Domain Layer (Pure Unit Tests):**

```csharp
public class NotificationTests
{
    [Fact]
    public void Create_WithValidData_ReturnsNotification()
    {
        // Arrange
        var request = new SendNotificationRequest
        {
            UserId = "user-123",
            Channel = "email",
            TemplateKey = "order-confirmation"
        };
        
        // Act
        var notification = Notification.Create(request);
        
        // Assert
        notification.Should().NotBeNull();
        notification.Status.Should().Be(NotificationStatus.Pending);
    }
}
```

**Application Layer (Service Tests with Fakes):**

```csharp
public class NotificationServiceTests
{
    private readonly NotificationService _sut;
    private readonly FakeNotificationRepository _fakeRepo;
    private readonly FakeSendingService _fakeSender;
    
    public NotificationServiceTests()
    {
        _fakeRepo = new FakeNotificationRepository();
        _fakeSender = new FakeSendingService();
        _sut = new NotificationService(_fakeRepo, _fakeSender);
    }
    
    [Fact]
    public async Task SendAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new SendNotificationRequest { /* ... */ };
        
        // Act
        var result = await _sut.SendAsync(request);
        
        // Assert
        result.Success.Should().BeTrue();
        _fakeRepo.Notifications.Should().ContainSingle();
        _fakeSender.SentMessages.Should().ContainSingle();
    }
}
```

**Infrastructure Layer (Contract Tests with Sandbox):**

```csharp
public class TwilioSmsClientTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SendAsync_ValidMessage_SucceedsWithTwilioSandbox()
    {
        // Arrange
        var client = new TwilioSmsClient(/* sandbox credentials */);
        var message = new SmsMessage
        {
            To = "+15005550006", // Twilio magic number
            Body = "Test message"
        };
        
        // Act
        var result = await client.SendAsync(message);
        
        // Assert
        result.Success.Should().BeTrue();
    }
}
```

**API Layer (Minimal E2E with TestServer):**

```csharp
public class NotificationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public NotificationsControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task SendNotification_WithValidToken_Returns202()
    {
        // Arrange
        var request = new SendNotificationRequest { /* ... */ };
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", TestTokens.ValidAdminToken);
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/notifications", request);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }
}
```

---

### 23.8. Benefits and Trade-offs

**Benefits:**

✅ **Clear separation of concerns** - Each layer has a well-defined responsibility  
✅ **Testability** - Easy to unit test with fakes and mocks  
✅ **Performance** - No extra mediation layer (MediatR); direct service orchestration  
✅ **Simplicity** - Easier to reason about and debug  
✅ **Flexibility** - Swap implementations (e.g., switch from SendGrid to SES) without changing Application layer  

**Trade-offs:**

⚠️ **More wiring code** - Explicit DI registration and service dependencies  
⚠️ **Disciplined boundaries required** - Developers must respect dependency rules  
⚠️ **Less "magic"** - No automatic request/response pipelines (must build manually if needed)  

---

## 24. Maintenance & Future Enhancements (Long-Term and Technical Debt)

> **Note:** This section complements Section 19 (Routine Operations) and focuses on **long-term evolution, technical debt management, and observability roadmap**.

---

### 24.1. Long-Term Evolution

**Full CloudEvents Adoption:**

- **Goal:** Standardize all event payloads across Listo.Auth, Listo.Orders, Listo.RideSharing, and Listo.Notification using CloudEvents 1.0
- **Benefits:**
  - Interoperability and portability
  - Vendor-neutral event format
  - Easier integration with third-party event brokers and SaaS tools

**Shared Schema Registry:**

- **Goal:** Centralized schema registry (e.g., Azure Schema Registry or Confluent Schema Registry) for event definitions
- **Benefits:**
  - Schema versioning and evolution
  - Automatic validation of event payloads
  - Documentation and discoverability

**Outbox/Inbox Patterns:**

- **Goal:** Implement transactional outbox pattern for exactly-once semantics
- **Implementation:**
  - Outbox table stores events in same transaction as entity changes
  - Background worker publishes events to Service Bus
  - Inbox table deduplicates incoming events

```csharp
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var serviceBusSender = scope.ServiceProvider.GetRequiredService<ServiceBusSender>();
            
            // Fetch unprocessed events
            var events = await dbContext.OutboxEvents
                .Where(e => !e.Processed)
                .OrderBy(e => e.OccurredAt)
                .Take(100)
                .ToListAsync(stoppingToken);
            
            foreach (var evt in events)
            {
                // Publish to Service Bus
                var message = new ServiceBusMessage(evt.Payload)
                {
                    MessageId = evt.EventId,
                    ContentType = "application/cloudevents+json"
                };
                
                await serviceBusSender.SendMessageAsync(message, stoppingToken);
                
                // Mark as processed
                evt.Processed = true;
                evt.ProcessedAt = DateTime.UtcNow;
            }
            
            await dbContext.SaveChangesAsync(stoppingToken);
            
            // Poll every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

**Multi-Region Active-Active:**

- **Goal:** Deploy Listo.Notification in multiple Azure regions (e.g., East US, West Europe) with active-active configuration
- **Components:**
  - Azure Traffic Manager or Front Door for global load balancing
  - Regional SQL databases with replication or Cosmos DB multi-region writes
  - Regional Service Bus namespaces with geo-replication

**Provider-Agnostic Abstraction with Weighted Routing:**

- **Goal:** Abstract provider selection logic to support multiple SMS/Email providers with cost-aware routing
- **Example:**

```csharp
public class WeightedProviderSelector
{
    private readonly List<ProviderOption> _providers;
    
    public IEmailSender SelectEmailProvider()
    {
        // Weighted random selection based on cost and success rate
        var totalWeight = _providers.Sum(p => p.Weight);
        var random = Random.Shared.NextDouble() * totalWeight;
        
        double cumulative = 0;
        foreach (var provider in _providers)
        {
            cumulative += provider.Weight;
            if (random < cumulative)
            {
                return provider.Sender;
            }
        }
        
        return _providers.First().Sender;
    }
}

public class ProviderOption
{
    public IEmailSender Sender { get; set; }
    public double Weight { get; set; } // Higher = more likely to be selected
    public decimal CostPer1000 { get; set; }
    public double SuccessRate { get; set; }
}
```

---

### 24.2. Technical Debt Management

**Catalog TODOs in Code:**

Tag technical debt items with structured comments:

```csharp
// TODO [tech-debt] [due:2025-12-31] [priority:medium]
// Refactor TemplateRenderer to support Liquid templates in addition to Handlebars
public string RenderTemplate(Template template, object data)
{
    // Current: Basic string interpolation
    // Future: Full Liquid/Handlebars support
}
```

**Quarterly Architecture Review:**

- **Schedule:** Last week of each quarter
- **Participants:** Engineering team, architect, product owner
- **Topics:**
  - Performance bottlenecks and optimization opportunities
  - Cost analysis and optimization
  - Security posture review
  - Dependency updates and vulnerabilities
  - Technical debt prioritization

**Example Review Agenda:**

1. **Performance (30 min)**
   - Review P95/P99 latencies
   - Identify slow queries and optimize
   - Assess cache hit ratios

2. **Cost (20 min)**
   - Analyze Azure spend by resource
   - Identify underutilized resources
   - Review provider costs (Twilio, SendGrid)

3. **Security (20 min)**
   - Review Defender alerts
   - Check Key Vault access logs
   - Assess dependency vulnerabilities

4. **Tech Debt (30 min)**
   - Review cataloged TODOs
   - Prioritize based on impact and effort
   - Allocate capacity for next sprint

**Decompose Large Services:**

As scale grows, consider splitting into focused components:

- **Template Service:** Dedicated microservice for template management and rendering
- **Delivery Service:** Separate service for provider integrations
- **Analytics Service:** Real-time analytics and reporting

---

### 24.3. Observability Roadmap

**OpenTelemetry Adoption:**

**Goal:** Implement OpenTelemetry for distributed tracing, metrics, and logs end-to-end

**Implementation:**

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddSource("Listo.Notification")
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Listo.Notification")
            .AddAzureMonitorMetricExporter(options =>
            {
                options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
            });
    });

// Custom instrumentation
var activitySource = new ActivitySource("Listo.Notification");

using var activity = activitySource.StartActivity("SendNotification");
activity?.SetTag("notification.channel", "email");
activity?.SetTag("notification.tenant", tenantId);
```

**SLOs and Error Budgets:**

**Service Level Objectives (SLOs):**

| Service | SLO | Measurement Window |
|---------|-----|-----------------|
| API availability | 99.9% | 30 days |
| Email delivery time (P95) | < 30s | 7 days |
| SMS delivery time (P95) | < 10s | 7 days |
| Push delivery time (P95) | < 5s | 7 days |

**Error Budget:**

- **Formula:** `Error Budget = (1 - SLO) * Total Requests`
- **Example:** 99.9% SLO = 0.1% error budget = 1 error per 1000 requests

**Release Policy:**

- If error budget depleted: Freeze non-critical releases, focus on reliability
- If error budget healthy: Proceed with feature releases

**SLO Monitoring:**

```kusto
// Application Insights query for API availability SLO
requests
| where timestamp > ago(30d)
| summarize 
    Total = count(),
    Failed = countif(success == false),
    Availability = 100.0 * (1.0 - todouble(countif(success == false)) / count())
| extend SLO = 99.9
| extend ErrorBudget = (100.0 - SLO) * Total / 100.0
| extend ErrorBudgetRemaining = ErrorBudget - Failed
| project Total, Failed, Availability, SLO, ErrorBudgetRemaining
```

---

### 24.4. Backlog Candidates for Future Sprints

**Web-Based Template Editor:**

- **Features:**
  - WYSIWYG HTML editor for email templates
  - Live preview with sample data
  - Localization support (edit translations inline)
  - Version history and rollback
  - Template validation (syntax, placeholders)

**In-App Notification Center:**

- **Features:**
  - Persistent notification inbox (90-day retention)
  - Read/unread status sync across devices
  - Mark all as read
  - Push-to-inbox consolidation (reduce push spam)
  - Notification categories and filtering

**Provider Delivery Analytics:**

- **Features:**
  - Dashboard with delivery success rates per provider
  - Cost per channel and provider
  - Bounce and complaint rates (email)
  - Cohort analysis (e.g., delivery time by country)
  - A/B test results for template variations

**Rate-Limit Self-Service Controls:**

- **Features:**
  - Tenant admin portal for quota management
  - Real-time rate limit usage visualization
  - Temporary quota increase requests
  - Budget alerts configuration

---

**Summary:**  
This plan ensures a robust, secure, and scalable Notification & Communication Service API using modern .NET and Azure best practices, with SQL Server as the data store and full support for the requirements in `notification_api_endpoints.md`. The architecture supports push, SMS, email, in-app messaging, analytics, and user preferences, with integrations for FCM, Twilio, and SendGrid/Azure Communication
