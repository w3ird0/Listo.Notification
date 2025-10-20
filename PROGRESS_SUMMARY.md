# Listo.Notification Specifications - Progress Summary

**Date:** January 20, 2025  
**Branch:** `feature/notification-specs-update`  
**Commit:** `5523fd4`  
**Overall Completion:** ~25%

---

## 📋 Executive Summary

We've successfully completed **Phase 1** of the Listo.Notification service specifications, establishing the foundational architecture, integration patterns, and technology stack. The service is designed as a shared notification platform for the entire ListoExpress ecosystem, supporting multi-channel delivery (Push, SMS, Email, In-App Messaging) with comprehensive cost management, rate limiting, and GDPR compliance.

### Key Decisions Made

1. **No MediatR** - Direct service injection per architectural guidelines
2. **Azure Functions** for background processing (not Hangfire/Quartz)
3. **Dedicated SQL Server** database (not shared with other services)
4. **Triple integration pattern:** REST APIs + Azure Service Bus + Event-driven
5. **Synchronous delivery** for critical driver assignments, async for everything else
6. **24-hour idempotency window** with Redis caching
7. **Exponential backoff retry:** base 5s, factor 2, max 6 attempts, no dead-letter queues

---

## ✅ What's Been Completed

### 1. Document Structure & Foundation
- ✅ Created 24-section table of contents with logical flow
- ✅ Added cross-references to Listo.Auth, Listo.Orders, Listo.RideSharing docs
- ✅ Established naming conventions and tone matching existing services
- ✅ Created TODO.md tracking document for remaining work

### 2. Configuration Defaults (Section 1)
- ✅ **Retention Policies:**
  - Notification content: 90 days
  - In-app chat (Support): 180 days
  - In-app chat (Customer-Driver): 30 days
  - Audit logs: 13 months
  - Queue rows: Purged 30 days after completion

- ✅ **Retry Policy:**
  - Base delay: 5 seconds
  - Backoff factor: 2
  - Jitter: Enabled
  - Max attempts: 6
  - No dead-letter queues

- ✅ **Rate Limits:**
  - Per-user per channel: 60/hour with burst of 20
  - Per-service daily caps: Email 50k, SMS 10k, Push 200k
  - In-app messaging: Unlimited (under Redis token bucket)

- ✅ **Budget Thresholds:**
  - Alert at 80% and 100% per month per service/channel
  - Block non-critical notifications beyond budget
  - Admin override capability for critical flows

- ✅ **Idempotency:**
  - X-Idempotency-Key required on create/send POSTs
  - 24-hour uniqueness window per serviceOrigin
  - Redis cache with 24-hour TTL

- ✅ **Observability:**
  - OpenTelemetry tracing with W3C traceparent headers
  - X-Correlation-Id required for all requests

### 3. Technology Stack (Section 2)
- ✅ **Backend:** .NET 9, ASP.NET Core Web API, NO MediatR
- ✅ **Database:** Dedicated SQL Server with TDE
- ✅ **ORM:** Entity Framework Core 9.0
- ✅ **Azure Services:**
  - Azure Service Bus (queues + pub/sub topics)
  - Azure Functions (4 functions documented)
  - Azure SignalR Service (real-time messaging)
  - Azure Key Vault (secrets management)
  - Azure Cache for Redis (rate limiting, presence)
  - Azure Blob Storage (file uploads)
  - Azure Container Apps/AKS (hosting)
  - Azure Application Insights (monitoring)
- ✅ **External Providers:**
  - Firebase Cloud Messaging (FCM) for push
  - Twilio for SMS
  - SendGrid for email
- ✅ **Dev Tools:**
  - FluentValidation, Swashbuckle, xUnit, Moq, Testcontainers
  - Serilog, Docker, Bicep/Terraform

### 4. Service Integration & Architecture (Section 3)
- ✅ **Three Integration Patterns:**
  1. **Direct REST API** - Synchronous with JWT/X-Service-Secret auth
  2. **Azure Service Bus** - Async via queues and topics
  3. **Event-Driven** - Loosely coupled via domain events

- ✅ **Service Bus Structure:**
  - Queues: `listo-notifications-queue`, `-priority`, `-retry`
  - Topic: `listo-notifications-events`
  - Subscriptions: `auth-notifications`, `orders-notifications`, `ridesharing-notifications`

- ✅ **Message Envelope Format:** Complete JSON schema with all required fields

- ✅ **Sequence Diagrams:**
  - **Synchronous:** Driver assignment (Orders → NotifAPI → FCM + SignalR → Driver)
  - **Asynchronous:** Order status update (Orders → Service Bus → Function → SendGrid → Customer)

- ✅ **Service Origin Tracking:** `auth`, `orders`, `ridesharing`, `products`, `system`

- ✅ **Idempotency & Correlation:** Header requirements and behavior documented

### 5. Database Schema (Section 4 - In Progress)
- ✅ **Schema Overview:** Encryption (AES-256-GCM), TDE, Key Vault
- ✅ **Notifications Table:** Complete schema with 4 indexes

---

## 🚧 What's In Progress

### Database Schema Completion (Section 4 - 20% Done)
Still need to document:
- NotificationQueue table (with encrypted PII)
- RetryPolicy table
- CostTracking table + monthly rollup views
- RateLimiting configuration table
- AuditLog table
- Templates table (with versioning)
- Preferences table (with quiet hours)
- Conversations + Messages tables (in-app messaging)
- ERD diagram
- Migration strategy

---

## 📋 What Remains (75% of Work)

### High Priority (Foundation)
1. **Complete database schema** (Section 4)
2. **Authentication & Authorization** (Section 5) - JWT validation, secret management
3. **Service-specific event mappings** (Section 6) - Auth, Orders, RideSharing events
4. **Cost management & rate limiting** (Section 7) - Redis token bucket, quotas
5. **Notification delivery strategy** (Section 8) - Sync/async, retry, webhooks, templates

### Medium Priority (Core Features)
6. **Real-time messaging** (Section 9) - SignalR hub, WebSockets, presence
7. **API implementation** (Section 10) - Controllers, middleware, validation
8. **Azure Functions** (Section 14) - 4 functions with timers and concurrency
9. **Configuration management** (Section 15) - Key Vault, feature flags, appsettings

### Lower Priority (Operations)
10. **Deployment** (Section 17) - Service Bus setup, Functions, SignalR, Redis, SQL
11. **Monitoring & logging** (Section 18) - Application Insights, Serilog
12. **GDPR & compliance** (Section 22) - Retention, audit, export, deletion
13. **Clean Architecture** (Section 23) - Folder structure, no MediatR patterns

### Documentation Updates
14. **notification_api_endpoints.md** - Add internal endpoints, webhooks, batch ops, SignalR, admin endpoints
15. **Integration guides** - For Listo.Auth, Orders, RideSharing
16. **Service Bus message formats** - Canonical events and schemas
17. **Testing strategy** - Unit, integration, contract, load, chaos tests

---

## 🎯 Recommended Next Steps

### Immediate (Next Session)
1. **Complete Section 4** - Finish all database tables, add ERD
2. **Section 6** - Map all service events to notification templates
3. **Section 8** - Document delivery strategy with retry logic

### Short Term (Next 2-3 Sessions)
4. **Azure Functions configuration** (Section 14)
5. **Update notification_api_endpoints.md** with internal and webhook endpoints
6. **Add service-specific event payload examples**

### Medium Term (Next 4-6 Sessions)
7. Complete remaining NOTIFICATION_MGMT_PLAN.md sections (9-23)
8. Add comprehensive testing strategy
9. Document deployment procedures
10. Add GDPR compliance features

---

## 🔍 Key Integration Points

### Listo.Auth Integration
**Events to Handle:**
- `EmailVerificationRequested` → Send verification email
- `MobileVerificationRequested` → Send SMS OTP
- `PasswordResetRequested` → Send reset email + SMS
- `TwoFactorCodeIssued` → Send 2FA code via SMS/Email
- `SuspiciousLoginDetected` → Send security alert

**Headers Required:**
- `X-Service-Secret` from Key Vault
- `X-Correlation-Id` for tracing
- `X-Idempotency-Key` for deduplication

### Listo.Orders Integration
**Events to Handle:**
- `OrderConfirmed` → Email/Push to customer
- `OrderStatusUpdated` → Push/SMS to customer
- `DriverAssigned` → **SYNCHRONOUS** push to driver + SignalR broadcast
- `DeliveryCompleted` → Email receipt + push notification

**Critical Path:** Driver assignment MUST be synchronous (< 2 second response)

### Listo.RideSharing Integration
**Events to Handle:**
- `RideBooked` → Confirmation to customer
- `DriverAssigned` → **SYNCHRONOUS** push to driver + customer
- `DriverArriving` → Push to customer with ETA
- `RideCompleted` → Receipt email + push

**Critical Path:** Driver assignment MUST be synchronous

---

## 📊 Metrics & KPIs to Track

### Performance
- P95 latency for synchronous notifications (target: < 2s)
- P99 latency for asynchronous notifications (target: < 5s)
- Success rate by channel (target: > 99%)

### Cost
- Cost per notification by channel
- Monthly spend by service origin
- Budget utilization percentage

### Reliability
- Retry success rate
- Failed notification rate
- Provider failover events

### Compliance
- GDPR request response time
- Audit log completeness
- Data retention adherence

---

## 🛠️ Development Environment Setup

When ready to implement:

```bash
# Clone and setup
cd D:\OneDrive\Projects\ListoExpress\Dev\Listo.Notification
git checkout feature/notification-specs-update

# Recommended folder structure (to be created):
mkdir -p src/Listo.Notification.Domain
mkdir -p src/Listo.Notification.Application
mkdir -p src/Listo.Notification.Infrastructure
mkdir -p src/Listo.Notification.Api
mkdir -p src/Listo.Notification.Functions
mkdir -p tests/Listo.Notification.UnitTests
mkdir -p tests/Listo.Notification.IntegrationTests
```

### Required Azure Resources (for implementation)
- Azure SQL Database
- Azure Service Bus namespace
- Azure Functions app
- Azure SignalR Service
- Azure Cache for Redis
- Azure Key Vault
- Azure Container Registry
- Azure Application Insights

---

## 📞 Questions & Clarifications Resolved

All original questions from the requirements analysis have been answered and integrated into the specifications:

1. ✅ Service integration: All three patterns (REST, Service Bus, Events)
2. ✅ API endpoints: notification_api_endpoints.md exists, needs expansion
3. ✅ Authentication: JWT for clients, shared secrets for services
4. ✅ User context: Aligns with Listo.Auth user IDs
5. ✅ Notification events: Defined for Auth, Orders, RideSharing
6. ✅ Data consistency: Sync for critical, async for others, retry policies
7. ✅ In-app messaging: Customer-Support + Customer-Driver
8. ✅ Rate limiting: Per-user and per-service with Redis token bucket
9. ✅ Technology alignment: Matches Listo.Auth, no MediatR, Azure Functions
10. ✅ Database: Dedicated SQL Server with encrypted PII

---

## 📝 Notes & Assumptions

1. **Encryption:** PII (email, phone, FCM tokens) encrypted at rest with AES-256-GCM
2. **Keys:** All encryption keys and secrets in Azure Key Vault with rotation support
3. **Scaling:** Designed for horizontal scaling via Azure Container Apps
4. **Monitoring:** OpenTelemetry tracing + Application Insights + Serilog
5. **Testing:** Unit, integration, contract, load, and chaos testing planned
6. **Deployment:** IaC with Bicep or Terraform recommended

---

## 🔗 Related Resources

- [Listo.Auth Documentation](../Listo.Auth/docs/)
- [Listo.Orders Documentation](../Listo.Orders/docs/)
- [Listo.RideSharing Documentation](../Listo.RideSharing/)
- [Azure Service Bus Docs](https://learn.microsoft.com/en-us/azure/service-bus-messaging/)
- [Azure Functions Docs](https://learn.microsoft.com/en-us/azure/azure-functions/)
- [Azure SignalR Service Docs](https://learn.microsoft.com/en-us/azure/azure-signalr/)

---

**Created:** January 20, 2025  
**Author:** Warp AI Agent  
**Status:** Phase 1 Complete, Phase 2 In Progress
