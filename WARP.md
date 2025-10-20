# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Repository Context

- **Purpose:** Shared Notification & Communication Service for ListoExpress ecosystem (multi-channel: Push, SMS, Email, In-App Messaging, Real-Time)
- **Current State:** Specifications phase (no `.sln`/`.csproj` yet). Active branch: `feature/notification-specs-update`
- **Key Documents (read these first):**
  - `NOTIFICATION_MGMT_PLAN.md` → Complete architecture, integration patterns, Azure topology, database schema
  - `notification_api_endpoints.md` → API endpoint specifications, request/response formats, authentication
  - `PROGRESS_SUMMARY.md` and `TODO.md` → Current phase status, architectural decisions, next steps

## Common Commands (Copy-Paste Ready)

### Git Workflow

```powershell
# Inspect current state
git status
git branch --show-current
git fetch --all --prune

# Switch to feature branch
git switch feature/notification-specs-update

# Commit in phases (project rule: update TODO.md after each phase, then push)
git add -A
git commit -m "docs: phase X complete - describe what was done"
git push -u origin feature/notification-specs-update
```

### Documentation Workflow

```powershell
# Open specs in default editor (Windows)
start NOTIFICATION_MGMT_PLAN.md
start notification_api_endpoints.md
start TODO.md

# Quick view/search in terminal
type NOTIFICATION_MGMT_PLAN.md
Select-String -Path notification_api_endpoints.md -Pattern "/api/v1"
Select-String -Path NOTIFICATION_MGMT_PLAN.md -Pattern "^##+"  # Find all headings
```

### .NET Scaffolding (Execute when implementation begins)

```powershell
# Create solution structure
dotnet new sln -n Listo.Notification
mkdir src, tests

# Create projects (Clean Architecture layers)
dotnet new classlib -n Listo.Notification.Domain -o src/Listo.Notification.Domain
dotnet new classlib -n Listo.Notification.Application -o src/Listo.Notification.Application
dotnet new classlib -n Listo.Notification.Infrastructure -o src/Listo.Notification.Infrastructure
dotnet new webapi -n Listo.Notification.Api -o src/Listo.Notification.Api
dotnet new func -n Listo.Notification.Functions -o src/Listo.Notification.Functions  # Azure Functions
dotnet new xunit -n Listo.Notification.UnitTests -o tests/Listo.Notification.UnitTests
dotnet new xunit -n Listo.Notification.IntegrationTests -o tests/Listo.Notification.IntegrationTests

# Add all projects to solution
dotnet sln add src/Listo.Notification.Domain/Listo.Notification.Domain.csproj
dotnet sln add src/Listo.Notification.Application/Listo.Notification.Application.csproj
dotnet sln add src/Listo.Notification.Infrastructure/Listo.Notification.Infrastructure.csproj
dotnet sln add src/Listo.Notification.Api/Listo.Notification.Api.csproj
dotnet sln add src/Listo.Notification.Functions/Listo.Notification.Functions.csproj
dotnet sln add tests/Listo.Notification.UnitTests/Listo.Notification.UnitTests.csproj
dotnet sln add tests/Listo.Notification.IntegrationTests/Listo.Notification.IntegrationTests.csproj

# Set project references (Clean Architecture dependencies)
dotnet add src/Listo.Notification.Application/Listo.Notification.Application.csproj reference src/Listo.Notification.Domain/Listo.Notification.Domain.csproj
dotnet add src/Listo.Notification.Infrastructure/Listo.Notification.Infrastructure.csproj reference src/Listo.Notification.Application/Listo.Notification.Application.csproj src/Listo.Notification.Domain/Listo.Notification.Domain.csproj
dotnet add src/Listo.Notification.Api/Listo.Notification.Api.csproj reference src/Listo.Notification.Application/Listo.Notification.Application.csproj src/Listo.Notification.Infrastructure/Listo.Notification.Infrastructure.csproj
dotnet add src/Listo.Notification.Functions/Listo.Notification.Functions.csproj reference src/Listo.Notification.Application/Listo.Notification.Application.csproj src/Listo.Notification.Infrastructure/Listo.Notification.Infrastructure.csproj
dotnet add tests/Listo.Notification.UnitTests/Listo.Notification.UnitTests.csproj reference src/Listo.Notification.Domain/Listo.Notification.Domain.csproj src/Listo.Notification.Application/Listo.Notification.Application.csproj
dotnet add tests/Listo.Notification.IntegrationTests/Listo.Notification.IntegrationTests.csproj reference src/Listo.Notification.Api/Listo.Notification.Api.csproj src/Listo.Notification.Infrastructure/Listo.Notification.Infrastructure.csproj

# Restore dependencies
dotnet restore
```

### Build and Test

```powershell
# Build
dotnet build -c Debug
dotnet build -c Release

# Run all tests with coverage
dotnet test -c Release --collect "XPlat Code Coverage"

# Run specific test(s)
dotnet test --filter "Name~NotificationService"
dotnet test --filter "FullyQualifiedName=Listo.Notification.UnitTests.NotificationServiceTests.SendsPushNotification"

# Watch mode for tests
dotnet watch test --project tests/Listo.Notification.UnitTests
```

### Code Formatting and Analysis

```powershell
# Format code
dotnet format
dotnet format style
dotnet format analyzers

# Add to Directory.Build.props (repo root) for built-in analyzers:
# <Project>
#   <PropertyGroup>
#     <EnableNETAnalyzers>true</EnableNETAnalyzers>
#     <AnalysisMode>AllEnabledByDefault</AnalysisMode>
#   </PropertyGroup>
# </Project>
```

### Local Development

```powershell
# Run API locally
dotnet run --project src/Listo.Notification.Api

# Run with hot reload
dotnet watch run --project src/Listo.Notification.Api

# Run Azure Functions locally (requires Azure Functions Core Tools)
cd src/Listo.Notification.Functions
func start
```

## High-Level Architecture

### Purpose
Centralized notification and communication platform for the entire ListoExpress ecosystem, providing unified multi-channel delivery with cost management, rate limiting, and real-time messaging capabilities.

### Integration Patterns

#### 1. Direct REST API
- **Client Authentication:** JWT Bearer tokens (issued by Listo.Auth)
- **Service-to-Service:** `X-Service-Secret` header (shared secrets from Azure Key Vault)
- **Required Headers:**
  - `Authorization: Bearer {jwt}` OR `X-Service-Secret: {secret}`
  - `X-Idempotency-Key: {unique_key}` (required for POST operations)
  - `X-Correlation-Id: {trace_id}` (required for tracing)
  - `traceparent: {w3c_trace}` (OpenTelemetry)
- **Routing:** All routes prefixed with `/api/v1`
- **Synchronous vs Asynchronous:**
  - **Synchronous:** Driver assignment notifications (< 2s response time requirement)
  - **Asynchronous:** All other notifications via Azure Service Bus

#### 2. Azure Service Bus Integration
- **Queues:**
  - `listo-notifications-queue` (standard priority)
  - `listo-notifications-priority` (high priority, time-sensitive)
  - `listo-notifications-retry` (failed notifications)
- **Topic & Subscriptions:**
  - Topic: `listo-notifications-events`
  - Subscriptions: `auth-notifications`, `orders-notifications`, `ridesharing-notifications`

#### 3. Event-Driven Architecture
- Services publish domain events to Service Bus topic
- Listo.Notification subscribes and maps events to notification templates
- Processed based on user preferences and rate limits

### Azure Components

- **Azure Service Bus:** Message queuing and pub/sub
- **Azure Functions:** 4 background processors (scheduled, retry, cost calculator, data retention)
- **Azure SignalR Service:** Real-time in-app messaging and presence
- **Azure SQL Database:** Dedicated instance with TDE (Transparent Data Encryption)
- **Azure Key Vault:** Secrets, API keys, encryption keys
- **Azure Cache for Redis:** Rate limiting (token bucket), typing indicators, presence
- **Azure Blob Storage:** File uploads for messaging
- **Azure Application Insights:** Monitoring and distributed tracing
- **Azure Container Apps / AKS:** Container hosting

### Technology Stack

- **.NET 9** (ASP.NET Core Web API)
- **Entity Framework Core 9.0** (Code-First migrations)
- **NO MediatR** (architectural decision - use direct service injection)
- **FluentValidation:** Request validation
- **Serilog:** Structured logging
- **Swashbuckle:** OpenAPI/Swagger documentation
- **xUnit, Moq, FluentAssertions, Testcontainers:** Testing

### External Providers

- **Firebase Cloud Messaging (FCM):** Push notifications
- **Twilio:** SMS delivery
- **SendGrid:** Email delivery

### Clean Architecture Layers

```
Domain/              # Entities, value objects, domain events (no dependencies)
Application/         # Business logic, interfaces, DTOs (depends on Domain)
Infrastructure/      # EF Core, external services, Azure SDK (depends on Application, Domain)
Api/                 # Controllers, middleware, filters (depends on Application, Infrastructure)
Functions/           # Azure Functions for background jobs (depends on Application, Infrastructure)
```

### Key Architectural Constraints

- **No MediatR:** Per project guidelines - use direct service injection and repository patterns
- **RESTful Design:** Resource-oriented endpoints, standard HTTP methods and status codes
- **Versioned Routes:** All routes prefixed with `/api/v1`
- **Idempotency:** 24-hour window per service origin, stored in Redis
- **Rate Limiting:**
  - Per-user per channel: 60/hour with burst of 20
  - Per-service daily caps: Email 50k, SMS 10k, Push 200k
  - Redis token bucket implementation
- **Retry Policy:**
  - Base delay: 5 seconds
  - Backoff factor: 2 (exponential)
  - Jitter enabled
  - Max attempts: 6
  - No dead-letter queues

### Data Model Overview

- **Notifications:** Immutable audit log of all sent notifications
- **NotificationQueue:** Pending notifications with encrypted PII (email, phone, FCM tokens)
- **Devices:** User device registrations for push notifications
- **Templates:** Versioned notification templates with variable substitution
- **Conversations & Messages:** In-app messaging (customer-driver, customer-support)
- **Preferences:** User notification preferences with quiet hours
- **CostTracking:** Per-service cost attribution and budget management
- **AuditLog:** Compliance and GDPR tracking

### Service Integration Points

- **Listo.Auth:** Email verification, password reset, 2FA, suspicious login alerts
- **Listo.Orders:** Order confirmations, status updates, driver assignments, delivery completed
- **Listo.RideSharing:** Ride bookings, driver assignments, driver arriving, ride completed

All notifications track `serviceOrigin` for cost attribution, rate limiting, and analytics.

## Repository-Specific Notes

1. **Project Rule:** After every set of changes, update `TODO.md` with progress and push to remote
2. **Solution/Project Naming:** Use exact names listed above for consistency across Listo services
3. **Branch Strategy:** Work on `feature/notification-specs-update` for specifications phase
4. **Default Configuration Values:** Defined in `NOTIFICATION_MGMT_PLAN.md` Section 1
5. **No External Rule Files:** No CLAUDE.md, .cursorrules, or .github/copilot-instructions.md detected at present

## Next Steps

When moving from specifications to implementation:

1. Run the `.NET scaffolding` commands above to create solution structure
2. Implement database schema from `NOTIFICATION_MGMT_PLAN.md` Section 4 using EF Core migrations
3. Create domain entities in `Domain/` project (immutable, no dependencies)
4. Implement service interfaces in `Application/` project
5. Implement Azure integrations in `Infrastructure/` project (Service Bus, Redis, Key Vault, etc.)
6. Build API controllers in `Api/` project following REST conventions
7. Create Azure Functions in `Functions/` project for background processing
8. Write comprehensive tests (unit, integration, contract testing)
9. Configure CI/CD pipeline for automated builds and deployments
10. After each phase: update `TODO.md` and push changes

## Related Services

- `../Listo.Auth/` - Authentication and authorization service
- `../Listo.Orders/` - Order management service
- `../Listo.RideSharing/` - Ride-hailing service
- `../Listo.Products/` - Product catalog service
