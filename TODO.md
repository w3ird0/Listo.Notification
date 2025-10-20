# Listo.Notification Specifications Update - TODO

## ✅ Completed Tasks

### Phase 1: Foundation & Architecture (Completed)
- [x] Create feature branch `feature/notification-specs-update`
- [x] Establish default configuration values (retention, retry, rate limits, budgets)
- [x] Update NOTIFICATION_MGMT_PLAN.md table of contents with 24 sections
- [x] Add cross-references to related Listo services documentation
- [x] Update Section 2: Technology Stack
  - [x] Replace Hangfire with Azure Functions
  - [x] Add Azure Service Bus, SignalR Service, Redis
  - [x] Document NO MediatR usage
  - [x] List all Azure services and external providers
- [x] Create Section 3: Service Integration & Architecture
  - [x] Document three integration patterns (REST, Service Bus, Events)
  - [x] Define authentication headers (JWT for clients, X-Service-Secret for services)
  - [x] Specify Service Bus queues and topics structure
  - [x] Add sequence diagrams for sync and async flows
  - [x] Document idempotency and correlation requirements
- [x] Start Section 4: Database Schema
  - [x] Add schema overview with encryption and TDE
  - [x] Begin Notifications table specification with indexes
- [x] Create WARP.md file
  - [x] Document common git/dotnet commands for Windows/PowerShell
  - [x] Add .NET scaffolding commands for Clean Architecture setup
  - [x] Document high-level architecture and integration patterns
  - [x] List Azure components and technology stack
  - [x] Include project-specific conventions (No MediatR, versioned routes, etc.)

---

## ✅ Completed Tasks

### Phase 1: Foundation & Architecture (Completed)
- [x] Create feature branch `feature/notification-specs-update`
- [x] Establish default configuration values (retention, retry, rate limits, budgets)
- [x] Update NOTIFICATION_MGMT_PLAN.md table of contents with 24 sections
- [x] Add cross-references to related Listo services documentation
- [x] Update Section 2: Technology Stack
  - [x] Replace Hangfire with Azure Functions
  - [x] Add Azure Service Bus, SignalR Service, Redis
  - [x] Document NO MediatR usage
  - [x] List all Azure services and external providers
- [x] Create Section 3: Service Integration & Architecture
  - [x] Document three integration patterns (REST, Service Bus, Events)
  - [x] Define authentication headers (JWT for clients, X-Service-Secret for services)
  - [x] Specify Service Bus queues and topics structure
  - [x] Add sequence diagrams for sync and async flows
  - [x] Document idempotency and correlation requirements
- [x] Create WARP.md file
  - [x] Document common git/dotnet commands for Windows/PowerShell
  - [x] Add .NET scaffolding commands for Clean Architecture setup
  - [x] Document high-level architecture and integration patterns
  - [x] List Azure components and technology stack
  - [x] Include project-specific conventions (No MediatR, versioned routes, etc.)

### Phase 2: Database & Data Models (✅ COMPLETE - 2025-01-20)
- [x] **Complete Section 4: Database Schema**
  - [x] Notifications table (4.1)
  - [x] NotificationQueue table with encrypted PII (4.2)
  - [x] RetryPolicy table (4.3)
  - [x] CostTracking table with monthly rollup views (4.4)
  - [x] RateLimiting configuration table (4.5)
  - [x] AuditLog table for compliance (4.6)
  - [x] Templates table with versioning (4.7)
  - [x] Preferences table with quiet hours (4.8)
  - [x] Conversations and Messages tables for in-app messaging (4.9-4.10)
  - [x] Devices table (4.11)
  - [x] Add ERD diagram (4.12)
  - [x] Document migration strategy (4.13)
  - [x] Add completion status badge and cross-references

---

## 🚧 In Progress

### Phase 3: Core Service Logic (Current Priority)

#### ✅ Completed in Session 1 (2025-01-20)
- [x] Created .NET 9 Clean Architecture solution structure
- [x] Set up project dependencies (Domain → Application → Infrastructure → API/Functions)
- [x] Confirmed multi-tenancy database schema documentation is complete
- [x] Created IMPLEMENTATION_STATUS.md tracking document

#### 🔄 Next Session: Core Implementation Files
- [ ] Create all Domain entities, enums, and value objects
- [ ] Implement NotificationDbContext with EF Core 9 and tenant scoping
- [ ] Build RedisTokenBucketLimiter with embedded Lua script
- [ ] Implement notification provider integrations (Twilio, SendGrid, FCM)
- [ ] Create SignalR Hub with Redis backplane

---

## 📋 Remaining Tasks

### Phase 3: Core Service Logic (Not Started)
- [ ] **Section 5: Authentication & Authorization**
  - [ ] JWT validation from Listo.Auth
  - [ ] Service-to-service shared secret management
  - [ ] Key rotation guidance
  - [ ] HTTPS, HSTS enforcement
  - [ ] Input validation and request limits

- [ ] **Section 6: Service-Specific Event Mappings**
  - [ ] Listo.Auth events (EmailVerification, PasswordReset, 2FA, SuspiciousLogin)
  - [ ] Listo.Orders events (OrderConfirmed, StatusUpdated, DriverAssigned, DeliveryCompleted)
  - [ ] Listo.RideSharing events (RideBooked, DriverAssigned, DriverArriving, RideCompleted)
  - [ ] For each: channels, templateKey, variables, priority, sync/async, example payloads

- [ ] **Section 7: Cost Management & Rate Limiting**
  - [ ] Redis token bucket implementation
  - [ ] Per-user and per-service quotas
  - [ ] 429 responses with Retry-After headers
  - [ ] Budget tracking and alerting (80%, 100%)
  - [ ] Admin override capability

- [ ] **Section 8: Notification Delivery Strategy**
  - [ ] Synchronous delivery (driver assignment)
  - [ ] Asynchronous delivery via Service Bus
  - [ ] Exponential backoff retry policy with jitter
  - [ ] Webhook handlers (Twilio, SendGrid, FCM)
  - [ ] Template rendering and localization
  - [ ] Provider failover strategy

- [ ] **Section 9: Real-Time Messaging with SignalR**
  - [ ] Hub path `/hubs/messaging`
  - [ ] JWT authorization for WebSocket connections
  - [ ] Events: OnMessageReceived, OnTypingIndicator, OnMessageRead
  - [ ] Client methods: SendMessage, StartTyping, MarkAsRead
  - [ ] Presence and read receipts with Redis TTL

### Phase 4: Implementation Details (Not Started)
- [ ] **Section 10-13: API, Validation, File Uploads, Testing**
  - [ ] API implementation guidelines
  - [ ] Validation with FluentValidation
  - [ ] File upload to Azure Blob Storage
  - [ ] Testing strategy (unit, integration, contract, load, chaos)

- [ ] **Section 14: Azure Functions Configuration**
  - [ ] ScheduledNotificationRunner function
  - [ ] RetryProcessor function
  - [ ] CostAndBudgetCalculator function
  - [ ] DataRetentionCleaner function
  - [ ] Document timers and concurrency

- [ ] **Section 15: Configuration Management**
  - [ ] Azure Key Vault integration
  - [ ] Environment-specific appsettings
  - [ ] Feature flags
  - [ ] Startup validation

### Phase 5: Deployment & Operations (Not Started)
- [ ] **Section 16-18: Containerization, Deployment, Monitoring**
  - [ ] Dockerfile for .NET 9
  - [ ] Azure Service Bus setup (namespace, queues, topics, RBAC)
  - [ ] Azure Functions deployment
  - [ ] Azure SignalR Service configuration
  - [ ] Redis deployment and persistence
  - [ ] SQL migrations workflow
  - [ ] IaC with Bicep/Terraform
  - [ ] Application Insights and Serilog configuration

- [ ] **Section 19-20: Documentation & CI/CD**
  - [ ] Integration guides for each Listo service
  - [ ] Service Bus message format documentation
  - [ ] Rate limiting and cost management guides
  - [ ] OpenAPI/Swagger configuration
  - [ ] GitHub Actions CI/CD pipeline

- [ ] **Section 21-22: Security & Compliance**
  - [ ] Comprehensive security checklist
  - [ ] GDPR compliance features
  - [ ] Data retention and automated cleanup
  - [ ] User data export and deletion workflows
  - [ ] PII encryption at rest

- [ ] **Section 23-24: Architecture & Maintenance**
  - [ ] Clean Architecture folder structure (Domain, Application, Infrastructure, API, Functions)
  - [ ] No MediatR pattern documentation
  - [ ] Future enhancements roadmap

---

## 📝 notification_api_endpoints.md Updates

### Completed
- [x] Basic endpoint structure exists

### Remaining
- [ ] **Add API Conventions Section**
  - [ ] Versioned paths `/api/v1`
  - [ ] Authentication headers
  - [ ] Idempotency and correlation headers
  - [ ] Naming conventions

- [ ] **Document Service-to-Service Endpoints**
  - [ ] POST `/api/v1/internal/notifications/queue`
  - [ ] POST `/api/v1/internal/events/publish`
  - [ ] GET `/api/v1/internal/health`

- [ ] **Document Webhook Endpoints**
  - [ ] POST `/api/v1/webhooks/twilio/status`
  - [ ] POST `/api/v1/webhooks/sendgrid/events`
  - [ ] POST `/api/v1/webhooks/fcm/delivery-status`
  - [ ] Signature validation examples

- [ ] **Document Batch Operations**
  - [ ] POST `/api/v1/notifications/batch/send`
  - [ ] POST `/api/v1/notifications/batch/schedule`
  - [ ] GET `/api/v1/notifications/batch/{batchId}/status`

- [ ] **Document CRUD Endpoints**
  - [ ] Notifications resource (GET list, GET by ID, POST, PATCH)
  - [ ] Templates resource (full CRUD)
  - [ ] Preferences resource (GET, PUT, PATCH)
  - [ ] Pagination, filtering, sorting examples

- [ ] **Document SignalR Hub**
  - [ ] Hub endpoint `/hubs/messaging`
  - [ ] Connection negotiation flow
  - [ ] Events and client methods
  - [ ] Rate limiting for real-time messages

- [ ] **Document Admin Endpoints**
  - [ ] Rate limiting management
  - [ ] Cost management and budgets
  - [ ] Require admin role validation

- [ ] **Add Service Bus Message Formats**
  - [ ] Canonical message definitions
  - [ ] Application properties examples
  - [ ] Event payload examples

---

## 🎯 Next Steps (Priority Order)

1. **Complete database schema** (Section 4) - Foundation for everything else
2. **Service-specific event mappings** (Section 6) - Critical for integration
3. **Notification delivery strategy** (Section 8) - Core business logic
4. **Azure Functions configuration** (Section 14) - Background processing
5. **Update notification_api_endpoints.md** - Developer-facing documentation
6. **Testing, deployment, and compliance sections** - Production readiness

---

## 📊 Progress Summary

- **Overall Progress:** ~25% complete
- **NOTIFICATION_MGMT_PLAN.md:** Sections 1-3 complete, Section 4 in progress
- **notification_api_endpoints.md:** Basic structure exists, needs comprehensive updates
- **Estimated Completion:** Requires 4-6 more focused work sessions

---

## 🔄 Commit Strategy

When ready to commit:
```bash
git add .
git commit -m "feat: comprehensive notification service specifications

- Complete sections 1-3 of NOTIFICATION_MGMT_PLAN.md
- Add service integration architecture with REST, Service Bus, Events
- Document technology stack with Azure Functions, SignalR, Redis
- Begin database schema with Notifications table
- Establish defaults for retention, retry, rate limits, budgets

Relates to: Listo.Auth, Listo.Orders, Listo.RideSharing integration"

git push origin feature/notification-specs-update
```

---

**Last Updated:** 2025-01-20  
**Branch:** `feature/notification-specs-update`  
**Status:** Active Development
