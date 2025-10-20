# Implementation Plan - Clarified Decisions Applied

**Date:** 2025-01-20  
**Status:** Ready for Implementation  
**Branch:** `docs/notification-specs-completion`

---

## ✅ Business Decisions Clarified

### 1. Budgets
**Decision:** Track BOTH per-tenant AND per-service  
**Implementation:**
- Add `TenantId` field to `CostTracking` table
- Create dual-level budget views in Section 7
- Alert thresholds at both tenant and service levels

### 2. Quotas  
**Decision:** Maximum caps approach  
**Implementation:**
- Hard limits per channel in `RateLimiting` table
- Enforce maximum caps with clear 429 responses
- Document cap values in Section 7

### 3. Retry Policy
**Decision:** Channel-specific tuning  
**Implementation:**
- OTP/SMS: maxAttempts=4, baseDelay=3s, maxBackoff=2m
- Email: maxAttempts=6, baseDelay=5s, maxBackoff=10m  
- Push: maxAttempts=3, baseDelay=2s, maxBackoff=5m
- Document in Section 8

### 4. Provider Failover
**Decision:** Configure secondary providers  
**Implementation:**
- SMS: Twilio (primary) → AWS SNS (secondary)
- Email: SendGrid (primary) → Azure Communication Services (secondary)
- Circuit breaker pattern per provider
- Document in Section 8

### 5. FCM Webhooks
**Decision:** Implement feedback mechanism  
**Implementation:**
- GCP Pub/Sub push subscription to `/api/v1/webhooks/fcm/events`
- Handle delivery receipts and engagement events
- Document signature validation in Section 8

### 6. SignalR
**Decision:** Native ASP.NET Core + Redis backplane  
**Implementation:**
- Use ASP.NET Core SignalR with Redis backplane
- Hub path: `/hubs/notifications`
- Scale-out configuration with StackExchange.Redis
- Document in Section 9

### 7. Pagination
**Decision:** Use defaults  
**Implementation:**
- Default page size: 50
- Maximum page size: 100
- Support both offset and cursor-based (future)
- Document in `notification_api_endpoints.md`

### 8. Data Retention
**Decision:** Use defined retention periods  
**Implementation:**
- Already defined in Section 1 (90/180/30/13 months)
- Enforce via Azure Functions cleanup job
- Document in Section 14

### 9. Security
**Decision:** JWT scopes + API key support  
**Implementation:**
- JWT scopes: `notifications:read`, `notifications:write`, `notifications:admin`
- API keys for internal service-to-service (optional alternative to X-Service-Secret)
- Document in Section 5 and API endpoints doc

### 10. CI/CD
**Decision:** Skip for now  
**Implementation:**
- Add placeholder in Section 15
- Note: "To be defined based on DevOps team preference"

### 11. Multitenancy
**Decision:** Implement tenant scoping  
**Implementation:**
- Add `TenantId` throughout (Notifications, CostTracking, Budgets, Quotas)
- Tenant-level isolation for data and budgets
- SignalR groups: `tenant:{tenantId}`
- Document across Sections 4, 7, 9

### 12. CloudEvents
**Decision:** Adopt for Service Bus  
**Implementation:**
- Use CloudEvents 1.0 specification
- JSON envelope with: id, type, source, subject, time, datacontenttype, data
- Document in Section 6 and API endpoints doc

---

## 📋 Implementation Steps

### Step 1: Update Database Schema (Section 4) with Multitenancy
- [ ] Add `TenantId` column to: Notifications, CostTracking, Preferences, RateLimiting
- [ ] Update indexes to include TenantId where applicable
- [ ] Document tenant isolation strategy

### Step 2: Create Section 7 - Cost Management & Rate Limiting
- [ ] Redis token bucket with Lua script
- [ ] Per-tenant AND per-service budget tracking
- [ ] Maximum caps enforcement
- [ ] Admin override with audit trail
- [ ] Mermaid diagrams

### Step 3: Create Section 8 - Notification Delivery Strategy
- [ ] Channel-specific retry policies (OTP/SMS: 4 attempts, Email: 6, Push: 3)
- [ ] Provider failover (Twilio→AWS SNS, SendGrid→ACS)
- [ ] Webhook handlers (Twilio, SendGrid, FCM with Pub/Sub)
- [ ] Template rendering with Scriban
- [ ] Mermaid diagrams

### Step 4: Enhance Section 9 - Real-Time Messaging
- [ ] Native ASP.NET Core SignalR + Redis
- [ ] Hub configuration and JWT auth with scopes
- [ ] Tenant-scoped groups: `tenant:{tenantId}`
- [ ] Presence and read receipts
- [ ] Code samples

### Step 5: Complete Sections 10-24
- [ ] Section 10: Testing Strategy
- [ ] Section 11: Azure Functions (4 functions + timers)
- [ ] Section 12: Configuration Management
- [ ] Section 13: Deployment Procedures
- [ ] Section 14: Monitoring & Alerting
- [ ] Section 15: CI/CD Pipeline (placeholder)
- [ ] Section 16: Security Checklist
- [ ] Section 17: GDPR/Privacy
- [ ] Section 18: Clean Architecture
- [ ] Section 19: Observability
- [ ] Section 20: Data Migration
- [ ] Section 21: Feature Flags
- [ ] Section 22: Disaster Recovery
- [ ] Section 23: RBAC
- [ ] Section 24: Future Enhancements

### Step 6: Expand notification_api_endpoints.md
- [ ] API conventions (pagination: default 50, max 100)
- [ ] JWT scopes per endpoint
- [ ] CloudEvents format for Service Bus
- [ ] All CRUD endpoints
- [ ] Batch operations
- [ ] Internal endpoints
- [ ] Webhook endpoints (FCM with Pub/Sub)
- [ ] Admin endpoints
- [ ] Tenant-scoped examples

### Step 7: Add Code Examples
- [ ] Redis token bucket (.NET 9)
- [ ] Retry with jitter (.NET 9)
- [ ] SignalR hub with Redis backplane
- [ ] Webhook validators (Twilio, SendGrid, FCM)
- [ ] Options classes with validation
- [ ] appsettings.json (with tenant config)

### Step 8: Add Mermaid Diagrams
- [ ] Rate limiting with tenant scope
- [ ] Budget threshold alerts (dual-level)
- [ ] Provider failover sequence
- [ ] Delivery orchestration
- [ ] SignalR presence/receipts

---

## 🔧 Technical Specifications

### Multi-Tenancy Model
```
Tenant Structure:
- TenantId: GUID
- All user data scoped by TenantId
- Budget tracking per (TenantId, ServiceOrigin, Channel)
- Quotas per (TenantId, ServiceOrigin, Channel)
- SignalR groups: tenant:{tenantId}
```

### JWT Scopes
```
- notifications:read - View own notifications
- notifications:write - Send notifications
- notifications:admin - Manage templates, quotas, budgets
- notifications:internal - Service-to-service (alternative to X-Service-Secret)
```

### CloudEvents Envelope
```json
{
  "specversion": "1.0",
  "id": "evt-uuid-123",
  "type": "com.listoexpress.orders.confirmed",
  "source": "https://orders.listoexpress.com",
  "subject": "orders/ORD-001",
  "time": "2024-01-15T10:30:00Z",
  "datacontenttype": "application/json",
  "data": { 
    "orderId": "ORD-001",
    "tenantId": "tenant-uuid",
    ...
  }
}
```

### Provider Failover
```
SMS:
  Primary: Twilio (API key in Key Vault)
  Secondary: AWS SNS (credentials in Key Vault)
  Circuit Breaker: 5 failures → open for 60s

Email:
  Primary: SendGrid (API key)
  Secondary: Azure Communication Services
  Circuit Breaker: 5 failures → open for 60s
```

### Channel-Specific Retry
```
OTP/SMS:
  maxAttempts: 4
  baseDelay: 3s
  backoffFactor: 2.0
  maxBackoff: 2m
  jitterMs: 1000

Email:
  maxAttempts: 6
  baseDelay: 5s
  backoffFactor: 2.0
  maxBackoff: 10m
  jitterMs: 1000

Push:
  maxAttempts: 3
  baseDelay: 2s
  backoffFactor: 2.0
  maxBackoff: 5m
  jitterMs: 500
```

### Pagination Defaults
```
Default page size: 50
Maximum page size: 100
Minimum page size: 1

Headers:
- X-Total-Count: 1234
- X-Page-Size: 50
- X-Page-Number: 3
- Link: </api/v1/notifications?page=4>; rel="next"
```

---

## 📝 Files to Modify

1. `NOTIFICATION_MGMT_PLAN.md`
   - Update Section 4 with multitenancy fields
   - Insert new Section 7 (Cost & Rate Limiting)
   - Insert new Section 8 (Delivery Strategy)
   - Enhance Section 9 (SignalR)
   - Add Sections 10-24
   - Renumber existing sections

2. `notification_api_endpoints.md`
   - Add API conventions
   - Document JWT scopes
   - Add CloudEvents format
   - Add tenant-scoped examples
   - Add pagination examples
   - Document all endpoints

3. `TODO.md`
   - Mark completed sections
   - Update remaining work
   - Add implementation checkpoints

4. `CURRENT_SESSION_SUMMARY.md`
   - Update with new progress

---

## ⏱️ Estimated Timeline

- **Step 1:** 30 minutes
- **Step 2:** 2 hours
- **Step 3:** 2.5 hours
- **Step 4:** 1.5 hours
- **Step 5:** 10 hours
- **Step 6:** 3 hours
- **Step 7:** 2 hours
- **Step 8:** 1 hour

**Total:** ~22.5 hours

---

**Ready to Execute:** Yes  
**Next Action:** Begin with Step 1 (Multitenancy updates to Section 4)
