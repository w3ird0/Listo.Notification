# Listo.Notification Service

**Version:** 0.1.0 (In Development)  
**Framework:** .NET 9  
**Architecture:** Clean Architecture (NO MediatR)  
**Status:** Foundation Complete ✅

---

## 📚 Documentation Index

### Planning & Specifications
| Document | Purpose | Status |
|----------|---------|--------|
| [NOTIFICATION_MGMT_PLAN.md](./NOTIFICATION_MGMT_PLAN.md) | Complete service specification (24 sections) | Sections 1-6 ✅ |
| [notification_api_endpoints.md](./notification_api_endpoints.md) | API endpoint documentation | Basic structure ✅ |
| [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) | 8-step implementation roadmap | Complete ✅ |

### Progress Tracking
| Document | Purpose | Status |
|----------|---------|--------|
| [IMPLEMENTATION_STATUS.md](./IMPLEMENTATION_STATUS.md) | Detailed progress tracking with time estimates | Active ✅ |
| [SESSION_1_SUMMARY.md](./SESSION_1_SUMMARY.md) | Session 1 accomplishments and decisions | Complete ✅ |
| [CURRENT_SESSION_SUMMARY.md](./CURRENT_SESSION_SUMMARY.md) | Always-updated session progress | Current ✅ |
| [TODO.md](./TODO.md) | Task checklist organized by phase | Updated ✅ |

---

## 🏗️ Project Structure

```
Listo.Notification/
├── Listo.Notification.sln                     # Solution file
├── src/
│   ├── Listo.Notification.Domain/             # Domain entities, enums, value objects
│   ├── Listo.Notification.Application/        # Interfaces, DTOs, services (NO MediatR)
│   ├── Listo.Notification.Infrastructure/     # Data access, providers, rate limiting
│   ├── Listo.Notification.API/                # Controllers, middleware, SignalR hubs
│   └── Listo.Notification.Functions/          # Azure Functions for background jobs
└── docs/                                      # All documentation files
```

**Clean Architecture Dependencies:**
```
Domain (no dependencies)
   ↑
Application
   ↑
Infrastructure
   ↑
API / Functions
```

---

## 🚀 Quick Start

### Prerequisites
- .NET 9 SDK
- SQL Server (local or Azure)
- Redis (for rate limiting and SignalR backplane)
- Azure Service Bus (optional for development)

### Build & Run
```powershell
# Navigate to project root
cd D:\OneDrive\Projects\ListoExpress\Dev\Listo.Notification

# Restore and build
dotnet build

# Run API (when implemented)
dotnet run --project src/Listo.Notification.API

# Run Azure Functions (when implemented)
dotnet run --project src/Listo.Notification.Functions
```

---

## 🎯 Implementation Status

### ✅ Completed (Session 1)
- Project structure and dependencies
- Database schema documentation with multi-tenancy
- Business logic clarifications
- Comprehensive tracking documents

### 🚧 In Progress
- Domain layer implementation
- Infrastructure layer (DbContext, providers, rate limiting)
- API layer (controllers, middleware, SignalR)
- Azure Functions (background jobs)

### ⏳ Planned
- Sections 7-9 documentation (Cost Management, Delivery Strategy, SignalR)
- Sections 10-24 documentation (Testing, Functions, Deployment, etc.)
- API endpoint documentation expansion
- Mermaid diagrams for all flows

**Overall Progress:** ~15%  
**Estimated Completion:** 18-22 hours remaining

---

## 🔧 Key Technical Decisions

| Decision | Choice |
|----------|--------|
| Architecture | Clean Architecture without MediatR |
| Multi-Tenancy | Single database with TenantId scoping |
| Message Format | CloudEvents 1.0 + Legacy format (both supported) |
| Rate Limiting | Redis token bucket with Lua scripts |
| Provider Failover | Twilio→AWS SNS, SendGrid→ACS |
| SignalR | Native ASP.NET Core with Redis backplane |
| Budget Tracking | Per-tenant AND per-service with currency support |
| Pagination | Default: 50, Max: 100, Cursor-based (future) |

---

## 📖 Key Concepts

### Multi-Tenancy
- Schema-based isolation with `TenantId` column
- Automatic tenant scoping via global query filters
- Tenant context extracted from JWT `tenant_id` claim
- Service-to-service calls include `X-Tenant-Id` header

### Notification Channels
- **Push:** Firebase Cloud Messaging (FCM)
- **SMS:** Twilio (primary) → AWS SNS (fallback)
- **Email:** SendGrid (primary) → Azure Communication Services (fallback)
- **In-App:** SignalR with Redis backplane

### Retry Policies (Channel-Specific)
- **OTP/SMS:** 4 attempts, 3s base delay, 2m max backoff
- **Email:** 6 attempts, 5s base delay, 10m max backoff
- **Push:** 3 attempts, 2s base delay, 5m max backoff

### JWT Scopes
- `notifications:read` - View own notifications
- `notifications:write` - Send notifications
- `notifications:admin` - Manage templates, quotas, budgets
- `notifications:internal` - Service-to-service

---

## 🔗 Related Services

- **Listo.Auth** - User authentication and JWT token issuance
- **Listo.Orders** - Food delivery orders (driver assignment, status updates)
- **Listo.RideSharing** - Ride booking and driver notifications
- **Listo.Products** - Product catalog notifications

---

## 📞 Support & Contact

For questions or clarifications during implementation:
1. Review [IMPLEMENTATION_STATUS.md](./IMPLEMENTATION_STATUS.md) for task details
2. Check [NOTIFICATION_MGMT_PLAN.md](./NOTIFICATION_MGMT_PLAN.md) for specifications
3. Reference [SESSION_1_SUMMARY.md](./SESSION_1_SUMMARY.md) for architectural decisions

---

**Last Updated:** 2025-01-20  
**Next Session:** Domain + Infrastructure implementation
