# Service-Specific Event Mappings

**Service:** Listo.Notification  
**Last Updated:** 2025-01-20

---

## Overview

This document defines the event-to-notification mappings for all Listo services. Each service publishes domain events to Azure Service Bus, and the Notification service consumes these events to send notifications via multiple channels.

**Integration Pattern:** Event-Driven Architecture  
**Service Bus Topic:** `listo-notifications-events`  
**Subscriptions:**
- `auth-notifications` - Listo.Auth events
- `orders-notifications` - Listo.Orders events
- `ridesharing-notifications` - Listo.RideSharing events

---

## 1. Listo.Auth Service Events

### 1.1. Email Verification

**Event:** `EmailVerificationRequested`

**Channels:** Email  
**Priority:** High  
**Delivery Mode:** Asynchronous  
**Template Key:** `email_verification`

**Variables:**
```json
{
  "userName": "John Doe",
  "verificationLink": "https://app.listoexpress.com/verify?token=abc123",
  "expiresIn": "24 hours"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-auth-001",
  "occurredAt": "2024-01-15T10:30:00Z",
  "messageType": "EmailVerificationRequested",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-abc-123",
  "idempotencyKey": "auth-verify-user-123-20240115",
  "priority": "high",
  "channels": ["email"],
  "templateKey": "email_verification",
  "data": {
    "userName": "John Doe",
    "email": "john.doe@example.com",
    "verificationLink": "https://app.listoexpress.com/verify?token=abc123",
    "expiresIn": "24 hours"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 1.2. Password Reset

**Event:** `PasswordResetRequested`

**Channels:** Email, SMS (if 2FA enabled)  
**Priority:** High  
**Delivery Mode:** Asynchronous  
**Template Key:** `password_reset`

**Variables:**
```json
{
  "userName": "John Doe",
  "resetLink": "https://app.listoexpress.com/reset?token=xyz789",
  "expiresIn": "1 hour"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-auth-002",
  "occurredAt": "2024-01-15T11:00:00Z",
  "messageType": "PasswordResetRequested",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-def-456",
  "idempotencyKey": "auth-reset-user-123-20240115",
  "priority": "high",
  "channels": ["email", "sms"],
  "templateKey": "password_reset",
  "data": {
    "userName": "John Doe",
    "email": "john.doe@example.com",
    "phoneNumber": "+1234567890",
    "resetLink": "https://app.listoexpress.com/reset?token=xyz789",
    "expiresIn": "1 hour"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 1.3. Two-Factor Authentication (2FA)

**Event:** `TwoFactorAuthenticationRequested`

**Channels:** SMS, Email (backup)  
**Priority:** High  
**Delivery Mode:** Synchronous (SMS), Asynchronous (Email)  
**Template Key:** `two_factor_code`

**Variables:**
```json
{
  "userName": "John Doe",
  "code": "123456",
  "expiresIn": "5 minutes"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-auth-003",
  "occurredAt": "2024-01-15T12:00:00Z",
  "messageType": "TwoFactorAuthenticationRequested",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-ghi-789",
  "idempotencyKey": "auth-2fa-user-123-20240115120000",
  "priority": "high",
  "channels": ["sms", "email"],
  "templateKey": "two_factor_code",
  "data": {
    "userName": "John Doe",
    "phoneNumber": "+1234567890",
    "email": "john.doe@example.com",
    "code": "123456",
    "expiresIn": "5 minutes",
    "synchronous": true
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 1.4. Suspicious Login Detected

**Event:** `SuspiciousLoginDetected`

**Channels:** Email, Push, SMS  
**Priority:** High  
**Delivery Mode:** Asynchronous  
**Template Key:** `suspicious_login_alert`

**Variables:**
```json
{
  "userName": "John Doe",
  "location": "New York, USA",
  "ipAddress": "192.168.1.1",
  "device": "iPhone 14 Pro",
  "timestamp": "2024-01-15T13:00:00Z"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-auth-004",
  "occurredAt": "2024-01-15T13:00:00Z",
  "messageType": "SuspiciousLoginDetected",
  "serviceOrigin": "auth",
  "userId": "user-uuid-123",
  "correlationId": "trace-jkl-012",
  "idempotencyKey": "auth-suspicious-user-123-20240115130000",
  "priority": "high",
  "channels": ["email", "push", "sms"],
  "templateKey": "suspicious_login_alert",
  "data": {
    "userName": "John Doe",
    "location": "New York, USA",
    "ipAddress": "192.168.1.1",
    "device": "iPhone 14 Pro",
    "timestamp": "2024-01-15T13:00:00Z"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

## 2. Listo.Orders Service Events

### 2.1. Order Confirmed

**Event:** `OrderConfirmed`

**Channels:** Email, Push, In-App  
**Priority:** Normal  
**Delivery Mode:** Asynchronous  
**Template Key:** `order_confirmed`

**Variables:**
```json
{
  "customerName": "Jane Smith",
  "orderId": "ORD-001",
  "restaurantName": "Burger Palace",
  "totalAmount": "$25.50",
  "estimatedDeliveryTime": "30-45 minutes"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-orders-001",
  "occurredAt": "2024-01-15T14:00:00Z",
  "messageType": "OrderConfirmed",
  "serviceOrigin": "orders",
  "userId": "user-uuid-456",
  "correlationId": "trace-mno-345",
  "idempotencyKey": "orders-confirmed-ORD-001",
  "priority": "normal",
  "channels": ["email", "push", "inApp"],
  "templateKey": "order_confirmed",
  "data": {
    "customerName": "Jane Smith",
    "orderId": "ORD-001",
    "restaurantName": "Burger Palace",
    "totalAmount": "$25.50",
    "estimatedDeliveryTime": "30-45 minutes",
    "orderDetails": [
      { "item": "Cheeseburger", "quantity": 2 },
      { "item": "Fries", "quantity": 1 }
    ]
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 2.2. Order Status Updated

**Event:** `OrderStatusChanged`

**Channels:** Push, In-App  
**Priority:** Normal  
**Delivery Mode:** Asynchronous  
**Template Key:** `order_status_updated`

**Variables:**
```json
{
  "customerName": "Jane Smith",
  "orderId": "ORD-001",
  "oldStatus": "confirmed",
  "newStatus": "preparing",
  "estimatedTime": "25 minutes"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-orders-002",
  "occurredAt": "2024-01-15T14:15:00Z",
  "messageType": "OrderStatusChanged",
  "serviceOrigin": "orders",
  "userId": "user-uuid-456",
  "correlationId": "trace-pqr-678",
  "idempotencyKey": "orders-status-ORD-001-preparing",
  "priority": "normal",
  "channels": ["push", "inApp"],
  "templateKey": "order_status_updated",
  "data": {
    "customerName": "Jane Smith",
    "orderId": "ORD-001",
    "oldStatus": "confirmed",
    "newStatus": "preparing",
    "estimatedTime": "25 minutes"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 2.3. Driver Assigned

**Event:** `DriverAssigned`

**Channels:** Push, In-App  
**Priority:** High  
**Delivery Mode:** **Synchronous** (critical path)  
**Template Key:** `driver_assigned`

**Variables:**
```json
{
  "customerName": "Jane Smith",
  "orderId": "ORD-001",
  "driverName": "Mike Johnson",
  "driverPhoto": "https://cdn.listo.com/drivers/mike.jpg",
  "vehicleInfo": "Toyota Camry - ABC123",
  "estimatedArrival": "20 minutes"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-orders-003",
  "occurredAt": "2024-01-15T14:30:00Z",
  "messageType": "DriverAssigned",
  "serviceOrigin": "orders",
  "userId": "user-uuid-456",
  "correlationId": "trace-stu-901",
  "idempotencyKey": "orders-driver-ORD-001-driver-789",
  "priority": "high",
  "channels": ["push", "inApp"],
  "templateKey": "driver_assigned",
  "data": {
    "customerName": "Jane Smith",
    "orderId": "ORD-001",
    "driverName": "Mike Johnson",
    "driverPhoto": "https://cdn.listo.com/drivers/mike.jpg",
    "vehicleInfo": "Toyota Camry - ABC123",
    "estimatedArrival": "20 minutes",
    "synchronous": true
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 2.4. Delivery Completed

**Event:** `DeliveryCompleted`

**Channels:** Email, Push, In-App  
**Priority:** Normal  
**Delivery Mode:** Asynchronous  
**Template Key:** `delivery_completed`

**Variables:**
```json
{
  "customerName": "Jane Smith",
  "orderId": "ORD-001",
  "deliveryTime": "14:45",
  "feedbackLink": "https://app.listoexpress.com/feedback/ORD-001"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-orders-004",
  "occurredAt": "2024-01-15T14:45:00Z",
  "messageType": "DeliveryCompleted",
  "serviceOrigin": "orders",
  "userId": "user-uuid-456",
  "correlationId": "trace-vwx-234",
  "idempotencyKey": "orders-completed-ORD-001",
  "priority": "normal",
  "channels": ["email", "push", "inApp"],
  "templateKey": "delivery_completed",
  "data": {
    "customerName": "Jane Smith",
    "orderId": "ORD-001",
    "deliveryTime": "14:45",
    "feedbackLink": "https://app.listoexpress.com/feedback/ORD-001"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

## 3. Listo.RideSharing Service Events

### 3.1. Ride Booked

**Event:** `RideBooked`

**Channels:** Email, Push, In-App  
**Priority:** Normal  
**Delivery Mode:** Asynchronous  
**Template Key:** `ride_booked`

**Variables:**
```json
{
  "passengerName": "Sarah Williams",
  "rideId": "RIDE-101",
  "pickupLocation": "123 Main St, New York",
  "dropoffLocation": "456 Park Ave, New York",
  "estimatedArrival": "5 minutes"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-rides-001",
  "occurredAt": "2024-01-15T15:00:00Z",
  "messageType": "RideBooked",
  "serviceOrigin": "ridesharing",
  "userId": "user-uuid-789",
  "correlationId": "trace-yza-567",
  "idempotencyKey": "rides-booked-RIDE-101",
  "priority": "normal",
  "channels": ["email", "push", "inApp"],
  "templateKey": "ride_booked",
  "data": {
    "passengerName": "Sarah Williams",
    "rideId": "RIDE-101",
    "pickupLocation": "123 Main St, New York",
    "dropoffLocation": "456 Park Ave, New York",
    "estimatedArrival": "5 minutes",
    "fare": "$15.00"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 3.2. Driver Assigned (RideSharing)

**Event:** `RideDriverAssigned`

**Channels:** Push, In-App  
**Priority:** High  
**Delivery Mode:** **Synchronous**  
**Template Key:** `ride_driver_assigned`

**Variables:**
```json
{
  "passengerName": "Sarah Williams",
  "rideId": "RIDE-101",
  "driverName": "David Brown",
  "driverPhoto": "https://cdn.listo.com/drivers/david.jpg",
  "driverRating": "4.9",
  "vehicleInfo": "Honda Accord - XYZ789",
  "estimatedArrival": "3 minutes"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-rides-002",
  "occurredAt": "2024-01-15T15:05:00Z",
  "messageType": "RideDriverAssigned",
  "serviceOrigin": "ridesharing",
  "userId": "user-uuid-789",
  "correlationId": "trace-bcd-890",
  "idempotencyKey": "rides-driver-RIDE-101-driver-456",
  "priority": "high",
  "channels": ["push", "inApp"],
  "templateKey": "ride_driver_assigned",
  "data": {
    "passengerName": "Sarah Williams",
    "rideId": "RIDE-101",
    "driverName": "David Brown",
    "driverPhoto": "https://cdn.listo.com/drivers/david.jpg",
    "driverRating": "4.9",
    "vehicleInfo": "Honda Accord - XYZ789",
    "estimatedArrival": "3 minutes",
    "synchronous": true
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 3.3. Driver Arriving

**Event:** `DriverArriving`

**Channels:** Push, In-App  
**Priority:** High  
**Delivery Mode:** Asynchronous  
**Template Key:** `driver_arriving`

**Variables:**
```json
{
  "passengerName": "Sarah Williams",
  "rideId": "RIDE-101",
  "driverName": "David Brown",
  "estimatedArrival": "1 minute"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-rides-003",
  "occurredAt": "2024-01-15T15:07:00Z",
  "messageType": "DriverArriving",
  "serviceOrigin": "ridesharing",
  "userId": "user-uuid-789",
  "correlationId": "trace-efg-123",
  "idempotencyKey": "rides-arriving-RIDE-101",
  "priority": "high",
  "channels": ["push", "inApp"],
  "templateKey": "driver_arriving",
  "data": {
    "passengerName": "Sarah Williams",
    "rideId": "RIDE-101",
    "driverName": "David Brown",
    "estimatedArrival": "1 minute"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

### 3.4. Ride Completed

**Event:** `RideCompleted`

**Channels:** Email, Push, In-App  
**Priority:** Normal  
**Delivery Mode:** Asynchronous  
**Template Key:** `ride_completed`

**Variables:**
```json
{
  "passengerName": "Sarah Williams",
  "rideId": "RIDE-101",
  "fare": "$15.00",
  "distance": "5.2 miles",
  "duration": "15 minutes",
  "feedbackLink": "https://app.listoexpress.com/feedback/RIDE-101"
}
```

**Example Payload:**
```json
{
  "eventId": "evt-rides-004",
  "occurredAt": "2024-01-15T15:25:00Z",
  "messageType": "RideCompleted",
  "serviceOrigin": "ridesharing",
  "userId": "user-uuid-789",
  "correlationId": "trace-hij-456",
  "idempotencyKey": "rides-completed-RIDE-101",
  "priority": "normal",
  "channels": ["email", "push", "inApp"],
  "templateKey": "ride_completed",
  "data": {
    "passengerName": "Sarah Williams",
    "rideId": "RIDE-101",
    "fare": "$15.00",
    "distance": "5.2 miles",
    "duration": "15 minutes",
    "feedbackLink": "https://app.listoexpress.com/feedback/RIDE-101"
  },
  "metadata": {
    "locale": "en-US",
    "timezone": "America/New_York"
  }
}
```

---

## 4. Event Processing Flow

### Asynchronous Event Processing

```
Listo.Auth/Orders/RideSharing
    ↓ Publish Event
Azure Service Bus Topic (listo-notifications-events)
    ↓ Subscription Filter
Notification Service Processor (Azure Function)
    ↓
1. Validate Event Schema
2. Check User Preferences
3. Check Rate Limits
4. Queue Notification(s)
5. Update Cost Tracking
    ↓
Notification Queue → Provider → User
```

### Synchronous Event Processing

```
Listo.Orders/RideSharing
    ↓ HTTP POST (Internal API)
Notification Service API (/api/v1/internal/notifications/queue)
    ↓
1. Validate Service Secret
2. Check Rate Limits
3. Send Immediately (No Queue)
    ↓ Parallel Delivery
FCM (Push) + SignalR (In-App)
    ↓
Response with Delivery Status
```

---

## 5. Template Variables Reference

### Common Variables (All Events)

| Variable | Type | Description |
|----------|------|-------------|
| `userName` / `customerName` / `passengerName` | string | User's display name |
| `locale` | string | User's preferred locale (e.g., en-US) |
| `timezone` | string | User's timezone |
| `correlationId` | string | Distributed tracing ID |

### Channel-Specific Constraints

| Channel | Max Subject Length | Max Body Length |
|---------|-------------------|-----------------|
| Email | 255 chars | Unlimited (HTML) |
| SMS | N/A | 160 chars (1 segment) |
| Push | 50 chars | 150 chars |
| In-App | Unlimited | Unlimited (Markdown) |

---

## 6. Service Bus Configuration

### Topic Filters

**Auth Subscription Filter:**
```sql
ServiceOrigin = 'auth'
```

**Orders Subscription Filter:**
```sql
ServiceOrigin = 'orders'
```

**RideSharing Subscription Filter:**
```sql
ServiceOrigin = 'ridesharing'
```

### Dead Letter Queue Handling

- **Max Delivery Count:** 6 attempts
- **Dead Letter on Expiration:** Yes
- **Dead Letter on Filter Evaluation Exception:** Yes
- **Monitoring:** Alert on DLQ message count > 10

---

## 7. Testing Event Mappings

### Test Event Publisher

```bash
# Publish test event to Service Bus
az servicebus topic message send \
  --namespace-name listo-servicebus \
  --topic-name listo-notifications-events \
  --body '{
    "eventId": "test-001",
    "occurredAt": "2024-01-15T10:00:00Z",
    "messageType": "OrderConfirmed",
    "serviceOrigin": "orders",
    "userId": "test-user-123",
    "templateKey": "order_confirmed",
    "channels": ["email"],
    "data": { "orderId": "TEST-001" }
  }' \
  --application-properties ServiceOrigin=orders MessageType=OrderConfirmed
```

---

**Next Steps:**
- Implement event processors (Azure Functions)
- Create notification templates for each event
- Configure Service Bus subscriptions with filters
- Set up monitoring and alerts for DLQ
