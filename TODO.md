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

#### ✅ Completed in Session 2 (2025-01-20)
- [x] Create all Domain entities, enums, and value objects
  - [x] RetryPolicyEntity with exponential backoff calculation
  - [x] RateLimitingEntity with tenant-scoped Redis keys
  - [x] ConversationType, MessageStatus, DevicePlatform, NotificationProvider enums
  - [x] Updated existing entities to use proper enums
- [x] Implement NotificationDbContext with EF Core 9 and tenant scoping
  - [x] Configured all 11 entities with proper indexes
  - [x] Tenant-scoped query filters for multi-tenancy
  - [x] Enum conversions to string for database storage
  - [x] Automatic timestamp management in SaveChangesAsync
  - [x] Created SeedData class for default retry policies and rate limits
- [x] Build RedisTokenBucketLimiter with embedded Lua script
  - [x] Atomic token bucket operations with Lua script
  - [x] Per-user and per-service rate limiting
  - [x] Hierarchical config lookup (tenant-specific → wildcard → global)
  - [x] Burst capacity support
  - [x] IRateLimiterService interface
- [x] Notification provider integrations (Twilio, SendGrid, FCM)
  - [x] Verified existing provider implementations
- [x] Create SignalR Hubs with Redis backplane support
  - [x] NotificationHub for real-time notification delivery
  - [x] MessagingHub for in-app conversations (customer-support, customer-driver)
  - [x] Typing indicators, read receipts, presence tracking
  - [x] Tenant and conversation-scoped groups

---

## 📋 Remaining Tasks

### Phase 3: Core Service Logic (In Progress)

#### ✅ Session 3 Completed (2025-01-20)
- [x] **Section 5: Authentication & Authorization** ✅ FULLY COMPLETE
  - [x] JWT validation from Listo.Auth
  - [x] Service-to-service shared secret management
  - [x] Key rotation guidance
  - [x] HTTPS, HSTS enforcement
  - [x] Input validation and request limits
  - [x] Created ServiceSecretAuthenticationMiddleware
  - [x] Created RequestValidationMiddleware
  - [x] Created AUTHENTICATION_CONFIGURATION.md
  - [x] Registered middlewares in Program.cs
  - [x] Added authorization policies (AdminOnly, SupportAccess, ServiceOnly, ManageTemplates, ManageBudgets)
  - [x] Configured HTTPS/HSTS for production
  - [x] Mapped SignalR hubs (/hubs/notifications, /hubs/messaging)

- [x] **Section 6: Service-Specific Event Mappings** ✅ COMPLETE
  - [x] Listo.Auth events (EmailVerification, PasswordReset, 2FA, SuspiciousLogin)
  - [x] Listo.Orders events (OrderConfirmed, StatusUpdated, DriverAssigned, DeliveryCompleted)
  - [x] Listo.RideSharing events (RideBooked, DriverAssigned, DriverArriving, RideCompleted)
  - [x] For each: channels, templateKey, variables, priority, sync/async, example payloads
  - [x] Created SERVICE_EVENT_MAPPINGS.md with 12 event definitions

- [x] **Section 7: Cost Management & Rate Limiting** ✅ FULLY COMPLETE
  - [x] Hierarchical rate limiting (user → service → tenant)
  - [x] Redis token bucket with burst capacity
  - [x] Per-user and per-service quotas
  - [x] 429 responses with Retry-After headers
  - [x] Budget tracking and alerting (80%, 100%)
  - [x] Admin override capability
  - [x] Priority-based budget enforcement (high-priority allowed at 100%)
  - [x] Created RateLimitingService with hierarchical checks (Application layer)
  - [x] Created BudgetEnforcementService (Application layer)
  - [x] Created RateLimitingMiddleware (API layer)
  - [x] Created BudgetMonitorFunction for Azure Functions (placeholder)
  - [x] Created COST_MANAGEMENT_RATE_LIMITING.md documentation
  - [x] ✅ **BUILD VERIFIED** - All projects compile successfully

#### ⏭️ Next Session

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

- **Overall Progress:** ~65% complete (increased from 50%)
- **NOTIFICATION_MGMT_PLAN.md:** Sections 1-7 complete/documented
- **Phase 1 (Foundation & Architecture):** ✅ Complete
- **Phase 2 (Database & Data Models):** ✅ Complete
- **Phase 3 (Core Service Logic):** 🔄 70% Complete
  - Domain entities and enums: ✅ Complete
  - EF Core DbContext with multi-tenancy: ✅ Complete
  - Redis rate limiter with Lua scripts: ✅ Complete
  - SignalR Hubs (Notifications + Messaging): ✅ Complete
  - Authentication & Authorization: ✅ FULLY COMPLETE (middleware + docs + Program.cs integration)
  - Service Event Mappings: ✅ Complete (12 events documented)
  - Cost Management & Rate Limiting: ✅ FULLY COMPLETE (hierarchical checks + budget enforcement + monitoring)
  - Delivery Strategy: ⏳ Remaining
  - SignalR Config: ⏳ Remaining
- **notification_api_endpoints.md:** Basic structure exists, needs comprehensive updates
- **Estimated Completion:** Requires 2-3 more focused work sessions

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

**Last Updated:** 2025-01-20 (Session 3 - Cost Management & Rate Limiting Complete)  
**Branch:** `feature/notification-specs-update`  
**Status:** Phase 3 70% Complete - Sections 5-7 Fully Done, Sections 8-9 Remaining
